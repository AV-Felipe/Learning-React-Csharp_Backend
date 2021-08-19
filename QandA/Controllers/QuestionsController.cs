using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QandA.Data;
using QandA.Data.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Text.Json;

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
        private readonly IQuestionCache _cache;

        public QuestionsController(IDataRepository dataRepository, IQuestionCache questionCache)
        {
            _dataRepository = dataRepository;
            _cache = questionCache;
        }

        //Action methods

        [HttpGet]
        public async Task<IEnumerable<QuestionGetManyResponse>> GetQuestions(string search, bool includeAnswers, int page=1, int pageSize=20) //o valor de search virá da query, na url (query parameters com o mesmo nome dos parâmetros dos action methods são automaticamente mapeados para o parâmetro do action method
        {
            if (string.IsNullOrEmpty(search))
            {
                if (includeAnswers)
                {
                    return await _dataRepository.GetQuestionsWithAnswers();
                }
                else
                {
                    return await _dataRepository.GetQuestions();
                }
                
            }
            else
            {
                return await _dataRepository.GetQuestionsBySearchWithPaging(search, page, pageSize);
            }

        }

        [HttpGet ("{questionId}")]//aqui o método get espera um parâmetro passado no caminho da url, no caso um id
        public async Task<ActionResult<QuestionGetSingleResponse>> GetQuestion(int questionId)//o id da url é o parâmetro aqui
        {
            //aqui estamos primeiro buscando a questão no cache, se ela não estiver lá, buscamos no servidor e a colocamos no cache
            var question = _cache.Get(questionId);
            if (question == null)
            {
                question = await _dataRepository.GetQuestion(questionId);
                if (question == null)
                {
                    return NotFound();
                }
                _cache.Set(question);
            }
            return question;
        }

        [HttpGet ("unanswered")]
        public async Task<IEnumerable<QuestionGetManyResponse>> GetUnansweredQuestions()
        {
            return await _dataRepository.GetUnansweredQuestions();
        }

        [Authorize]
        [HttpPost]
        public async Task<ActionResult<QuestionGetSingleResponse>> PostQuestion(QuestionPostRequest questionPostRequest)
        {
            var savedQuestion = await _dataRepository.PostQuestion(new QuestionPostFullRequest
            {
                Title = questionPostRequest.Title,
                Content = questionPostRequest.Content,
                UserId = "1",
                UserName = "felipe@supimpa.com",
                Created = DateTime.UtcNow
            });
            return CreatedAtAction(nameof(GetQuestion), new { questionId = savedQuestion.QuestionId }, savedQuestion);
        }

        [Authorize(Policy = "MustBeQuestionAuthor")]
        [HttpPut ("{questionId}")]
        public async Task<ActionResult<QuestionGetSingleResponse>> PutQuestion (int questionId, QuestionPutRequest questionPutRequest)
        {
            var question = await _dataRepository.GetQuestion(questionId);
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
            var savedQuestion = await _dataRepository.PutQuestion(questionId, questionPutRequest);
            _cache.Remove(savedQuestion.QuestionId);//remove a instância anterior existente no cache
            return savedQuestion;
        }

        [Authorize (Policy = "MustBeQuestionAuthor")]
        [HttpDelete ("{questionId}")]
        public async Task<ActionResult> DeleteQuestion(int questionId)
        {
            var question = await _dataRepository.GetQuestion(questionId);
            if(question == null)
            {
                return NotFound();
            }
            await _dataRepository.DeleteQuestion(questionId);
            _cache.Remove(questionId);//remove a instância anterior existente no cache
            return NoContent();
        }

        [Authorize]
        [HttpPost ("answer")]
        public async Task<ActionResult<AnswerGetResponse>> PostAnswer(AnswerPostRequest answerPostRequest)
        {
            var questionExists = await _dataRepository.QuestionExists(answerPostRequest.QuestionId.Value);
            if (!questionExists)
            {
                return NotFound();
            }
            var savedAnswer = await _dataRepository.PostAnswer(new AnswerPostFullRequest
            { 
                QuestionId = answerPostRequest.QuestionId.Value,
                Content = answerPostRequest.Content,
                UserId = "1",
                UserName = "felipe@supimpa.com",
                Created = DateTime.UtcNow
            });
            _cache.Remove(answerPostRequest.QuestionId.Value);//remove a instância anterior existente no cache
            return savedAnswer;
        }
        
    }
}
