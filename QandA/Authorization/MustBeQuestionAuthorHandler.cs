using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using QandA.Data;


namespace QandA.Authorization
{
    public class MustBeQuestionAuthorHandler: AuthorizationHandler<MustBeQuestionAuthorRequirement>
    {
        private readonly IDataRepository _dataRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public MustBeQuestionAuthorHandler(IDataRepository dataRepository, IHttpContextAccessor httpContextAccessor)
        {
            _dataRepository = dataRepository;
            _httpContextAccessor = httpContextAccessor;
        }

        protected async override Task HandleRequirementAsync(AuthorizationHandlerContext context, MustBeQuestionAuthorRequirement requirement)
        {
            //checar se p usuário está autenticado
            if (!context.User.Identity.IsAuthenticated)
            {
                context.Fail();
                return;
            }

            //obter o id da questão da request
            var questionId = _httpContextAccessor.HttpContext.Request.RouteValues["questionId"];
            int questionIdAsInt = Convert.ToInt32(questionId);

            //obter o id do usuário do name identifier claim
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier).Value;

            //obter a questão do repositório de dados
            var question = await _dataRepository.GetQuestion(questionIdAsInt);
            if (question == null)
            {
                //se for null, o controller retornará 404, de forma que não precisamos tratar isso aqui
                context.Succeed(requirement);//finaliza a execução aqui
                return;
            }

            //comparar o id de usuário da questão com o da requisição
            if (question.UserId != userId)
            {
                context.Fail();
                return;
            }
            context.Succeed(requirement);
        }
    }
}
