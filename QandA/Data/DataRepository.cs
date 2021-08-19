using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Dapper;
using QandA.Data.Models;
using static Dapper.SqlMapper;

namespace QandA.Data
{
    public class DataRepository : IDataRepository
    {
        //connection string como read only, para prevenir que seja alterada de fora da classe
        private readonly string _connectionString;

        //constructor para obter os dados da connection string do appsettings.json
        //o IConfiguration, instanciado em configuration, nos dá acesso aos itens do appsettings.json
        //entre colchetes passamos os campos de onde queremos obter a informação, separando os níveis por : (colons)
        public DataRepository (IConfiguration configuration)
        {
            _connectionString = configuration["ConnectionStrings:DefaultConnection"];
        }

        public async Task<AnswerGetResponse> GetAnswer(int answerId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                return await connection.QueryFirstOrDefaultAsync<
                    AnswerGetResponse>(
                    @"EXEC dbo.Answer_Get_ByAnswerId @AnswerId = @AnswerId",
                    new { AnswerId = answerId });
            }
        }
        public async Task<QuestionGetSingleResponse> GetQuestion(int questionId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                //query multiple: https://dapper-tutorial.net/querymultiple
                using (GridReader results = await connection.QueryMultipleAsync(@"EXEC dbo.Question_GetSingle @QuestionId = @QuestionId;
                EXEC dbo.Answer_Get_ByQuestionId @QuestionId = @QuestionId", new { QuestionId = questionId})
                )
                {
                    var question = (await results.ReadAsync<QuestionGetSingleResponse>()).FirstOrDefault();
                    if (question != null)
                    {
                        question.Answers = (await results.ReadAsync<AnswerGetResponse>()).ToList();
                    }
                    return question;
                }
            }
        }

        public async Task<IEnumerable<QuestionGetManyResponse>> GetQuestionsWithAnswers()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                //dapper multi-mapping - ver: https://github.com/DapperLib/Dapper#multi-mapping
                var questionDictionary = new Dictionary<int, QuestionGetManyResponse>();
                return (await connection.QueryAsync<QuestionGetManyResponse, AnswerGetResponse, QuestionGetManyResponse>(
                    "EXEC dbo.Question_GetMany_WithAnswers", map: (q, a) =>
                    {
                        QuestionGetManyResponse question;

                        if (!questionDictionary.TryGetValue(q.QuestionId, out question))
                        {
                            question = q;
                            question.Answers = new List<AnswerGetResponse>();
                            questionDictionary.Add(question.QuestionId, question);
                        }
                        question.Answers.Add(a);
                        return question;
                    },
                    splitOn: "QuestionId")
                    ).Distinct().ToList();
            }
        }

        public async Task<IEnumerable<QuestionGetManyResponse>> GetQuestions()
        {
            //pelo uso do bloco de código using, dentro do escopo de um método, temos um objeto
            //que é automaticamente descartado quando a execução sai do escopo, neste caso, uma conexão
            //com o servidor.
            //Nós utiizamos o SqlConnection, da biblioteca Microsoft SQL client, a qual é extendida pelo do Dapper
            //o método Query que usamos na connection vem do Dapper, e nos permite executar o Procedimento Armazenado
            //dbo.Question_GetMany do nosso servidor de SQL
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                return await connection.QueryAsync<QuestionGetManyResponse>(
                    @"EXEC dbo.Question_GetMany");
            }
        }

        public async Task<IEnumerable<QuestionGetManyResponse>> GetQuestionsBySearch(string search)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                return await connection.QueryAsync<QuestionGetManyResponse>(
                    @"EXEC dbo.Question_GetMany_BySearch @Search = @Search",
                    new { Search = search }); //aqui utilizamos um objeto anônimo (pelo new sem a declaração de um nome nem um tipo de variável). Por esse objeto passamos o valor de um parâmetro para o Dapper.
            }
        }

        public async Task<IEnumerable<QuestionGetManyResponse>> GetQuestionsBySearchWithPaging(string search, int pageNumber, int pageSize)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var parameters = new
                {
                    Search = search,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
                return await connection.QueryAsync<QuestionGetManyResponse>(@"EXEC dbo.Question_GetMany_BySearch_WithPaging @Search = @Search, @PageNumber = @PageNumber, @PageSize = @PageSize", parameters);
            }
        }

        public async Task<IEnumerable<QuestionGetManyResponse>> GetUnansweredQuestions()
        {
            using(var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                return await connection.QueryAsync<QuestionGetManyResponse>( @"EXEC dbo.Question_GetUnanswered");
            }
        }

        public async Task<IEnumerable<QuestionGetManyResponse>> GetUnansweredQuestionsAsync()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                return await connection.QueryAsync<QuestionGetManyResponse>("EXEC dbo.Question_GetUnanswered");
            }
        }

        public async Task<bool> QuestionExists(int questionId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                return await connection.QueryFirstAsync<bool>(
                    @"EXEC dbo.Question_Exists @QuestionId = @QuestionId",
                    new { QuestionId = questionId});
            }
        }

        public async Task<QuestionGetSingleResponse> PostQuestion (QuestionPostFullRequest question)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var questionId = await connection.QueryFirstAsync<int>(
                    @"EXEC dbo.Question_Post 
                    @Title = @Title, @Content = @Content, @UserId = @UserId, 
                    @UserName = @UserName, @Created = @Created", 
                    question
                    );
                return await GetQuestion(questionId);
            }
        }

        public async Task<QuestionGetSingleResponse> PutQuestion (int questionId, QuestionPutRequest question)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                //aqui utilizamos o método Execute do dapper, pois não vamos retornar nada do processo armazenado, apenas vamos executá-lo
                await connection.ExecuteAsync(
                    @"EXEC dbo.Question_Put @QuestionId = @QuestionId, @Title = @Title, @Content = @Content",
                    new { QuestionId = questionId, question.Title, question.Content}
                    );
                return await GetQuestion(questionId);
            }
        }

        public async Task DeleteQuestion(int questionId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                await connection.ExecuteAsync(
                    @"EXEC dbo.Question_Delete @QuestionId = @QuestionId",
                    new { QuestionId = questionId});
            }
        }

        public async Task<AnswerGetResponse> PostAnswer(AnswerPostFullRequest answer)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                return await connection.QueryFirstAsync<AnswerGetResponse>(
                    @"EXEC dbo.Answer_Post @QuestionId = @QuestionId, @Content = @Content,
                    @UserId = @UserId, @UserName = @UserName, @Created = @Created",
                    answer);
            }
        }
    }
}
