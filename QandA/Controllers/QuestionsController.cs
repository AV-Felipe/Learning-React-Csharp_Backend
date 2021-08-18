using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QandA.Data;
using QandA.Data.Models;

namespace QandA.Controllers
{
    [Route("api/[controller]")]//como nosso contoller se chama QuestionsController, [controller] será substituído por questions (nome do controller menos a palavra controller)
    [ApiController]
    public class QuestionsController : ControllerBase
    {
        //A classe DataRepository.cs é responsável pela conexão com o Banco de Dados
        //aqui acessaremos o BD por meio de uma instância dessa classe,
        //tal instância, no entanto, será gerada por injeção de dependência.
        //Diferentemente do IConfiguration, o IDataRepository precisa ser vinculado com a classe
        //do repositório no Startup.cs para que essa injeção funcione
        private readonly IDataRepository _dataRepository;

        public QuestionsController(IDataRepository dataRepository)
        {
            _dataRepository = dataRepository;
        }

        //Action methods

        [HttpGet]
        public IEnumerable<QuestionGetManyResponse> GetQuestions(string search, bool includeAnswers, int page=1, int pageSize=20) //o valor de search virá da query, na url (query parameters com o mesmo nome dos parâmetros dos action methods são automaticamente mapeados para o parâmetro do action method
        {
            if (string.IsNullOrEmpty(search))
            {
                if (includeAnswers)
                {
                    return _dataRepository.GetQuestionsWithAnswers();
                }
                else
                {
                    return _dataRepository.GetQuestions();
                }
                
            }
            else
            {
                return _dataRepository.GetQuestionsBySearchWithPaging(search, page, pageSize);
            }

        }

        [HttpGet ("{questionId}")]//aqui o método get espera um parâmetro passado no caminho da url, no caso um id
        public ActionResult<QuestionGetSingleResponse> GetQuestion(int questionId)//o id da urrl é o parâmetro aqui
        {
            var question = _dataRepository.GetQuestion(questionId);
            if (question == null)
            {
                return NotFound();
            }
            return question;
        }

        [HttpGet ("unanswered")]
        public IEnumerable<QuestionGetManyResponse> GetUnansweredQuestions()
        {
            return _dataRepository.GetUnansweredQuestions();
        }

        [HttpPost]
        public ActionResult<QuestionGetSingleResponse> PostQuestion(QuestionPostRequest questionPostRequest)
        {
            var savedQuestion = _dataRepository.PostQuestion(new QuestionPostFullRequest
            {
                Title = questionPostRequest.Title,
                Content = questionPostRequest.Content,
                UserId = "1",
                UserName = "felipe@supimpa.com",
                Created = DateTime.UtcNow
            });
            return CreatedAtAction(nameof(GetQuestion), new { questionId = savedQuestion.QuestionId }, savedQuestion);
        }

        [HttpPut ("{questionId}")]
        public ActionResult<QuestionGetSingleResponse> PutQuestion (int questionId, QuestionPutRequest questionPutRequest)
        {
            var question = _dataRepository.GetQuestion(questionId);
            if(question == null)
            {
                return NotFound();
            }
            questionPutRequest.Title = string.IsNullOrEmpty(questionPutRequest.Title) ?
                question.Title :
                questionPutRequest.Title;
            questionPutRequest.Content = string.IsNullOrEmpty(questionPutRequest.Content) ?
                question.Content :
                questionPutRequest.Content;
            var savedQuestion = _dataRepository.PutQuestion(questionId, questionPutRequest);
            return savedQuestion;
        }

        [HttpDelete ("{questionId}")]
        public ActionResult DeleteQuestion(int questionId)
        {
            var question = _dataRepository.GetQuestion(questionId);
            if(question == null)
            {
                return NotFound();
            }
            _dataRepository.DeleteQuestion(questionId);
            return NoContent();
        }

        [HttpPost ("answer")]
        public ActionResult<AnswerGetResponse> PostAnswer(AnswerPostRequest answerPostRequest)
        {
            var questionExists = _dataRepository.QuestionExists(answerPostRequest.QuestionId.Value);
            if (!questionExists)
            {
                return NotFound();
            }
            var savedAnswer = _dataRepository.PostAnswer(new AnswerPostFullRequest
            { 
                QuestionId = answerPostRequest.QuestionId.Value,
                Content = answerPostRequest.Content,
                UserId = "1",
                UserName = "felipe@supimpa.com",
                Created = DateTime.UtcNow
            });
            return savedAnswer;
        }
        
    }
}
