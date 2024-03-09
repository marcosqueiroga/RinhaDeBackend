using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using NpgsqlTypes;
using System.Text.Json;

namespace RinhaDeBackend
{
    public sealed class Program
    {
        private static readonly string[] TIPO_TRANSACAO = ["c", "d"];

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddNpgsqlDataSource(Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") ?? throw new ArgumentNullException("DB_CONNECTION_STRING"));

            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
                options.SerializerOptions.TypeInfoResolverChain.Insert(0, CustomJsonContext.Default);
            });

            var app = builder.Build();

            app.MapPost("/clientes/{id}/transacoes", async Task<Results<Ok<ResponseTransacao>, NotFound, UnprocessableEntity, StatusCodeHttpResult>> (HttpContext httpContext, NpgsqlConnection connection, int id) =>
            {
                Transacao transacao;

                try
                {
                    transacao = await httpContext.Request.ReadFromJsonAsync<Transacao>();

                    if (transacao.Valor <= 0
                        || string.IsNullOrEmpty(transacao.Tipo)
                        || transacao.Tipo.Length > 1
                        || Array.IndexOf(TIPO_TRANSACAO, transacao.Tipo) < 0
                        || string.IsNullOrEmpty(transacao.Descricao)
                        || transacao.Descricao.Length > 10)
                    {
                        return TypedResults.UnprocessableEntity();
                    }
                }
                catch
                {
                    return TypedResults.UnprocessableEntity();
                }

                await connection.OpenAsync();

                using var command = new NpgsqlCommand("SELECT public.insere_transacao($1, $2, $3)", connection)
                {
                    Parameters =
                    {
                        new NpgsqlParameter<int>()
                        {
                            NpgsqlDbType = NpgsqlDbType.Integer
                        },
                        new NpgsqlParameter<int>()
                        {
                            NpgsqlDbType = NpgsqlDbType.Integer
                        },
                        new NpgsqlParameter<string>()
                        {
                            NpgsqlDbType = NpgsqlDbType.Varchar,
                            Size = 10
                        }
                    }
                };

                command.Parameters[0].Value = id;
                command.Parameters[1].Value = transacao!.Tipo == "c" ? transacao.Valor : transacao.Valor * -1;
                command.Parameters[2].Value = transacao.Descricao;

                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var record = reader.GetFieldValue<object[]>(0);

                    if (record.Length == 1)
                    {
                        int result = (int)record[0];

                        if (result == -1)
                            return TypedResults.NotFound();
                        else if (result == -2)
                            return TypedResults.UnprocessableEntity();
                        else
                            throw new InvalidOperationException("insere_transacao invalid result");
                    }

                    (int limite, int saldo) = ((int)record[0], (int)record[1]);

                    return TypedResults.Ok(new ResponseTransacao(limite, saldo));
                }
                else
                    throw new InvalidOperationException("insere_transacao fail");
            });

            app.MapGet("/clientes/{id}/extrato", async Task<Results<Ok<ResponseExtrato>, NotFound, StatusCodeHttpResult>> (NpgsqlConnection connection, int id) =>
            {
                await connection.OpenAsync();

                using var command = new NpgsqlCommand(@"SELECT c.limite, c.saldo, t.valor, t.descricao, t.realizada_em
                                                          FROM public.clientes c
                                                          LEFT JOIN public.transacoes t ON c.id = t.id_cliente
                                                         WHERE c.id = $1
                                                         ORDER BY t.realizada_em DESC
                                                         LIMIT 10", connection)
                {
                    Parameters =
                    {
                        new NpgsqlParameter<int>()
                        {
                            NpgsqlDbType = NpgsqlDbType.Integer
                        }
                    }
                };

                command.Parameters[0].Value = id;

                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    DateTime? dataExtrato = null;
                    var transacoes = new List<Transacao>();

                    int limite = reader.GetInt32(0);
                    int total = reader.GetInt32(1);

                    if (!reader.IsDBNull(2))
                    {
                        do
                        {
                            int valor = reader.GetInt32(2);
                            string tipo = valor < 0 ? "d" : "c";

                            var transacao = new Transacao()
                            {
                                Valor = Math.Abs(valor),
                                Tipo = tipo,
                                Descricao = reader.GetString(3),
                                Realizada_Em = reader.GetDateTime(4)
                            };

                            transacoes.Add(transacao);
                        } while (await reader.ReadAsync());

                        if (transacoes.Count > 0)
                            dataExtrato = transacoes[0].Realizada_Em;
                    }

                    return TypedResults.Ok(new ResponseExtrato(new ResponseSaldo(total, limite, dataExtrato), transacoes));
                }
                else
                    return TypedResults.NotFound();
            });

            Console.WriteLine("API started!");

            app.Run();
        }
    }
}