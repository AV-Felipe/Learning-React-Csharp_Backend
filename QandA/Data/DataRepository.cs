using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Dapper;
using QandA.Data.Models;

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

        public AnswerGetResponse GetAnswer(int answerId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                return connection.QueryFirstOrDefault<
                    AnswerGetResponse>(
                    @"EXEC dbo.Answer_Get_ByAnswerId @AnswerId = @AnswerId",
                    new { AnswerId = answerId });
            }
        }
        public QuestionGetSingleResponse GetQuestion(int questionId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                var question = connection.QueryFirstOrDefault<
                    QuestionGetSingleResponse>(
                    @"EXEC dbo.Question_GetSingle @QuestionId = @QuestionId",
                    new { QuestionId = questionId});
                if (question != null)
                {
                    question.Answers = connection.Query<AnswerGetResponse>(
                        @"EXEC dbo.Answer_Get_ByQuestionId @QuestionId = @QuestionId",
                        new { QuestionId = questionId });
                }
                return question;
            }
        }

        public IEnumerable<QuestionGetManyResponse> GetQuestionsWithAnswers()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                var questionDictionary = new Dictionary<int, QuestionGetManyResponse>();
                return connection.Query<QuestionGetManyResponse, AnswerGetResponse, QuestionGetManyResponse>(
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
                    splitOn: "QuestionId"
                    ).Distinct().ToList();
            }
        }

        public IEnumerable<QuestionGetManyResponse> GetQuestions()
        {
            //pelo uso do bloco de código using, dentro do escopo de um método, temos um objeto
            //que é automaticamente descartado quando a execução sai do escopo, neste caso, uma conexão
            //com o servidor.
            //Nós utiizamos o SqlConnection, da biblioteca Microsoft SQL client, a qual é extendida pelo do Dapper
            //o método Query que usamos na connection vem do Dapper, e nos permite executar o Procedimento Armazenado
            //dbo.Question_GetMany do nosso servidor de SQL
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                return connection.Query<QuestionGetManyResponse>(
                    @"EXEC dbo.Question_GetMany");
            }
        }

        public IEnumerable<QuestionGetManyResponse> GetQuestionsBySearch(string search)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                return connection.Query<QuestionGetManyResponse>(
                    @"EXEC dbo.Question_GetMany_BySearch @Search = @Search",
                    new { Search = search }); //aqui utilizamos um objeto anônimo (pelo new sem a declaração de um nome nem um tipo de variável). Por esse objeto passamos o valor de um parâmetro para o Dapper.
            }
        }

        public IEnumerable<QuestionGetManyResponse> GetUnansweredQuestions()
        {
            using(var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                return connection.Query<QuestionGetManyResponse>(
                    @"EXEC dbo.Question_GetUnanswered");
            }
        }

        public bool QuestionExists(int questionId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                return connection.QueryFirst<bool>(
                    @"EXEC dbo.Question_Exists @QuestionId = @QuestionId",
                    new { QuestionId = questionId});
            }
        }

        public QuestionGetSingleResponse PostQuestion (QuestionPostFullRequest question)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                var questionId = connection.QueryFirst<int>(
                    @"EXEC dbo.Question_Post 
                    @Title = @Title, @Content = @Content, @UserId = @UserId, 
                    @UserName = @UserName, @Created = @Created", 
                    question
                    );
                return GetQuestion(questionId);
            }
        }

        public QuestionGetSingleResponse PutQuestion (int questionId, QuestionPutRequest question)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                //aqui utilizamos o método Execute do dapper, pois não vamos retornar nada do processo armazenado, apenas vamos executá-lo
                connection.Execute(
                    @"EXEC dbo.Question_Put @QuestionId = @QuestionId, @Title = @Title, @Content = @Content",
                    new { QuestionId = questionId, question.Title, question.Content}
                    );
                return GetQuestion(questionId);
            }
        }

        public void DeleteQuestion(int questionId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                connection.Execute(
                    @"EXEC dbo.Question_Delete @QuestionId = @QuestionId",
                    new { QuestionId = questionId});
            }
        }

        public AnswerGetResponse PostAnswer(AnswerPostFullRequest answer)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                return connection.QueryFirst<AnswerGetResponse>(
                    @"EXEC dbo.Answer_Post @QuestionId = @QuestionId, @Content = @Content,
                    @UserId = @UserId, @UserName = @UserName, @Created = @Created",
                    answer);
            }
        }
    }
}
