using QandA.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QandA.Data
{
    public interface IDataRepository
    {
        IEnumerable<QuestionGetManyResponse> GetQuestions();
        IEnumerable<QuestionGetManyResponse>
            GetQuestionsBySearch(string search);

        IEnumerable<QuestionGetManyResponse>
            GetUnansweredQuestions();

        QuestionGetSingleResponse
            GetQuestion(int questionId);

        bool QuestionExists(int questionId);

        AnswerGetResponse GetAnswer(int answerId);
    }
}
