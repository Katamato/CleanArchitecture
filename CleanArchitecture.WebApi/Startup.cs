﻿using Akka.Actor;
using Akka.Configuration;
using CleanArchitecture.AkkaNET.Interfaces;
using CleanArchitecture.AkkaNET.Providers;
using CleanArchitecture.Application.Customers.Queries.GetCustomerDetail;
using CleanArchitecture.Application.Infrastructure;
using FluentValidation.AspNetCore;
using MediatR;
using MediatR.Pipeline;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;

namespace CleanArchitecture.WebApi
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
            // akka.net
            var config = ConfigurationFactory.ParseString(GetHocon());
            var actorSystem = ActorSystem.Create("clean-arch-system", config);
            services.AddSingleton<IActorRefFactory>(actorSystem);
            services.AddSingleton<ICustomerActorProvider, CustomerActorProvider>();
            services.AddSingleton<IEmployeeActorProvider, EmployeeActorProvider>();

            // mediatr
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestPreProcessorBehavior<,>));
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestValidationBehavior<,>));
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestPerformanceBehaviour<,>));
            services.AddMediatR(typeof(GetCustomerDetailQueryHandler));

            // redis
            services.AddStackExchangeRedisCache(option =>
            {
                option.Configuration = "127.0.0.1";
                option.InstanceName = "master";
            });

            // setup
            services
                .AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_1)
                .AddFluentValidation(fv => fv.RegisterValidatorsFromAssemblyContaining<GetCustomerDetailQueryValidator>());

            // swagger
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info { Title = nameof(WebApi), Version = "v1" });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", nameof(WebApi));
                });
            }
            else
            {
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseMvc();
        }

        private static string GetHocon()
        {
            return @"akka.persistence{
                journal {
                  plugin = ""akka.persistence.journal.sql-server""
                  sql-server {
                      class = ""Akka.Persistence.SqlServer.Journal.SqlServerJournal, Akka.Persistence.SqlServer""
                      schema-name = dbo
                      auto-initialize = on
                      table-name = EventJournal
                      metadata-table-name = EventMetadata 
                      connection-string = ""Data Source=localhost\\SQLEXPRESS;Database=CleanArchitecture_CQRS;Integrated Security=True""
                  }
                }
              }
            ";
        }
    }
}
