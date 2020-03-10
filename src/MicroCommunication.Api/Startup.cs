using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MicroCommunication.Api.Authentication;
using MicroCommunication.Api.Hubs;
using MicroCommunication.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using RandomNameGeneratorLibrary;

namespace MicroCommunication.Api
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        readonly bool useApiKey;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            useApiKey = !string.IsNullOrEmpty(configuration["ApiKey"]);
            Console.WriteLine("Using API Key: " + useApiKey);
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            if (useApiKey)
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = ApiKeyAuthenticationOptions.DefaultScheme;
                    options.DefaultChallengeScheme = ApiKeyAuthenticationOptions.DefaultScheme;
                }).AddApiKeySupport(options =>
                {
                    options.ApiKeyHeaderName = "api-key";
                    options.ApiKey = "";
                });
            }

            services.AddSingleton(new HistoryService(Configuration["MongoDbConnectionString"]));

            // Create random name for testing session affinity
            var personGenerator = new PersonNameGenerator();
            var name = personGenerator.GenerateRandomFirstName();
            Configuration["RandomName"] = name;
            Console.WriteLine("My name is " + Configuration["RandomName"]);

            // Enforce lowercase routes
            services.AddRouting(options => options.LowercaseUrls = true);

            services.AddControllers();
            var signalR = services.AddSignalR();
            if (!string.IsNullOrEmpty(Configuration["RedisCacheConnectionString"]))
            {
                signalR.AddStackExchangeRedis(Configuration["RedisCacheConnectionString"]);
            }

            // Swagger
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("1.0", new OpenApiInfo
                {
                    Title = "Random API ",
                    Version = "1.0",
                    Description = $"An API for generating random numbers.\nMy name is {Configuration["RandomName"]}."
                });
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);

                if (useApiKey)
                {
                    c.AddSecurityDefinition("API Key", new OpenApiSecurityScheme
                    {
                        Description = "Add the key to access this API to the HTTP header of your requests.",
                        Name = "api-key",
                        In = ParameterLocation.Header,
                        Type = SecuritySchemeType.ApiKey
                    });
                    c.AddSecurityRequirement(new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Id = "API Key",
                                    Type = ReferenceType.SecurityScheme
                                }
                            }, new List<string>()
                        }
                    });
                }
            });

            // CORS
            services.AddCors(options => options.AddPolicy("CorsPolicy", builder =>
            {
                builder
                    .WithOrigins("http://localhost:4200")
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();

                if (!string.IsNullOrEmpty(Configuration["Cors"]))
                    builder.WithOrigins(Configuration["Cors"]);
            }));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            if (useApiKey)
            {
                app.UseApiKey(c =>
                {
                    c.ApiKeyHeaderName = "api-key";
                    c.ApiKey = Configuration["ApiKey"];
                });
            }

            app.UseRouting();

            app.UseCors("CorsPolicy");

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<ChatHub>("/chat");
            });

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/1.0/swagger.json", "Version 1.0");
                c.RoutePrefix = string.Empty;
            });
        }
    }
}
