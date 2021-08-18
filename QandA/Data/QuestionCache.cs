using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using QandA.Data.Models;

namespace QandA.Data
{
    public class QuestionCache : IQuestionCache
    {
        //Criação da memória cache
        private MemoryCache _cache { get; set; }
        public QuestionCache()
        {
            _cache = new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = 100 //limite do cache definido para 100 itens
            });
        }

        //Método para recuperar uma questão do cache
        private string GetCacheKey(int questionId) => $"Question - {questionId}";

        public QuestionGetSingleResponse Get (int questionId)
        {
            QuestionGetSingleResponse question;
            _cache.TryGetValue(GetCacheKey(questionId), out question);
            return question;
        }

        //Método para adiconar questões ao cache
        public void Set (QuestionGetSingleResponse question)
        {
            var cacheEntryOptions = new MemoryCacheEntryOptions().SetSize(1);//faz a amarração com o tamanho do definido para o cache (100)
            _cache.Set(GetCacheKey(question.QuestionId), question, cacheEntryOptions);
        }

        //Método para remover itens do cache
        public void Remove (int questionId)
        {
            _cache.Remove(GetCacheKey(questionId));
        }
    }
}
