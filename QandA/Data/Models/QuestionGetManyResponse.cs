using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QandA.Data.Models
{
    public class QuestionGetManyResponse
    {
        //nesta classe utilizamos os mesmos nomes dos campos retornados pelo Procedimento Armazenado
        //dbo.Question_GetMany, o que permitirá ao Dapper mapear os dados do BD de forma automática
        //para essa classe.
        //Campos retornados pelo procedimento, mas aqui ausentes, são ignorados
        public int QuestionId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string UserName { get; set; }
        public DateTime Created { get; set; }
        public List<AnswerGetResponse> Answers { get; set; }
    }
}
