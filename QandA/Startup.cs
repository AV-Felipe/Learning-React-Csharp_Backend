using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DbUp;
using QandA.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using QandA.Authorization;

namespace QandA
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var connectionString = Configuration.GetConnectionString("DefaultConnection");

            EnsureDatabase.For.SqlDatabase(connectionString); //DbUp: caso o database referido pela conection string n�o exista, esse ser� criado

            //Aqui estmos comparando o banco de dados com os c�digos SQL salvos em nosso projeto
            var upgrader = DeployChanges.To.SqlDatabase(connectionString, null).WithScriptsEmbeddedInAssembly(
                System.Reflection.Assembly.GetExecutingAssembly()).WithTransaction().Build();
            //caso hajam diferen�as entre o c�digo �mbu�do no projeto e o banco de dados, o c�digo � implementado
            if (upgrader.IsUpgradeRequired())
            {
                upgrader.PerformUpgrade();
            }

            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "QandA", Version = "v1" });
            });

            //ralacionado com o uso do db por inje��o de depend�ncia,
            //sempre que IDataRpository for referido em um construtor, ser� criada, caso j� n�o exista,
            //uma inst�ncia do DataRepository
            services.AddScoped<IDataRepository, DataRepository>();

            //disponibilizando o cache de perguntas para uso com DI
            //instanciado como singleton, de forma que diferentes requisi��es acessem a mesma inst�ncia
            services.AddMemoryCache();
            services.AddSingleton<IQuestionCache, QuestionCache>();

            //adiciona autentica��o JWT-based especificando a autoridade e audi�ncia conforme o appsettings.json
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                options.Authority = Configuration["Auth0:Authority"];
                options.Audience = Configuration["Auth0:Audience"];
            });

            //disponibiliza o cliente http para podermos chamar o web service do auth0
            services.AddHttpClient();
            //adiciona nossa pol�tica de autoriza��o
            services.AddAuthorization(options => options.AddPolicy("MustBeQuestionAuthor", policy => policy.Requirements.Add(new MustBeQuestionAuthorRequirement())));
            //registro do handler para nossas requisi��es, para ser usado por inje��o de depend�ncia
            services.AddScoped<IAuthorizationHandler, MustBeQuestionAuthorHandler>();
            //registro do http context accessor para ser usado por inje��o de dependencia e possibilitar a classe de verifica��o (MustBeQuestionAuthor) acessar os pedidos http para verificar a quest�o que est� sendo requerida
            services.AddHttpContextAccessor(); //equivale a AddSingleton<IHttpContextAccessor, HttpContextAccessor>

            //define uma pol�tica de CORS (Cross-Origin Resource Sharing) que permite � origins especificadas no apsettings.json o acesso da API
            services.AddCors(options => options.AddPolicy("CorsPolicy", builder => builder
            .AllowAnyMethod()
            .AllowAnyHeader()
            .WithOrigins(Configuration["Frontend"])));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "QandA v1"));
            }
            else
            {
                app.UseHttpsRedirection(); //n�o queremos esse middleware no modo de desenvolvimento, usaremos http no front e no back no modo de desenvolvimento - o firefox pode apresentar problemas se o backend estiver com https e o front http (protocolos distintos)
            }


            app.UseRouting();

            app.UseCors("CorsPolicy");

            //� importante que tanto o use authentication quanto o authorization estejam antes do endpoints, de outra forna os endopoints protegidos nunca receberam as pol�ticas de usu�rio, ficando inacess�veis mesmo para requests com um token v�lido
            app.UseAuthentication(); //middleware Microsoft.AspNetCore.Authentication.JwtBearer

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
