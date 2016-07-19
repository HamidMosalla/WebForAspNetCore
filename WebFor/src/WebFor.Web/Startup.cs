﻿using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WebFor.Core.Domain;
using WebFor.Infrastructure;
using WebFor.Infrastructure.EntityFramework;
using WebFor.Infrastructure.Services.Shared;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using cloudscribe.Web.Pagination;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using WebFor.DependencyInjection.Modules;
using WebFor.DependencyInjection.Modules.Article;
using WebFor.DependencyInjection.Modules.SiteOrder;
using WebFor.Infrastructure.Services.Shared;
using WebFor.Web.Mapper;
using WebFor.Web.Services;

namespace WebFor.Web
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            // Set up configuration sources.
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            if (env.IsDevelopment())
            {
                // For more details on using the user secret store see http://go.microsoft.com/fwlink/?LinkID=532709
                builder.AddUserSecrets();
            }

            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {

            services.AddDbContext<WebForDbContext>(options =>
                options.UseSqlServer(Configuration["Data:DefaultConnection:ConnectionString"]));

            services.AddIdentity<ApplicationUser, IdentityRole>(o =>
            {
                o.Password.RequireDigit = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequireUppercase = false;
                o.Password.RequiredLength = 6;
            }).AddEntityFrameworkStores<WebForDbContext>()
              .AddDefaultTokenProviders();

            services.AddMvc();

            services.AddMemoryCache();

            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.CookieName = ".WebFor";
            });

            services.Configure<AuthMessageSenderSecrets>(Configuration.GetSection("AuthMessageSenderSecrets"));

            services.AddTransient<IUrlHelperFactory, UrlHelperFactory>();
            
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton<IActionContextAccessor, ActionContextAccessor>();
            services.AddTransient<IBuildPaginationLinks, PaginationLinkBuilder>();
            services.AddSingleton<IConfiguration>(Configuration);

            // Autofac container configuration and modules
            var containerBuilder = new ContainerBuilder();

            containerBuilder.RegisterModule<UnitOfWorkModule>();
            containerBuilder.RegisterModule<AuthMessageSenderModule>();
            containerBuilder.RegisterModule<WebForDbContextSeedDataModule>();
            containerBuilder.RegisterModule<CkEditorFileUploderModule>();
            containerBuilder.RegisterModule<FileUploderModule>();
            containerBuilder.RegisterModule<FileDeleterModule>();
            containerBuilder.RegisterModule<FileUploadValidatorModule>();
            containerBuilder.RegisterModule<ArticleCreatorModule>();
            containerBuilder.RegisterModule<ArticleEditorModule>();
            containerBuilder.RegisterModule<PriceSpecCollectionFactoryModule>();
            containerBuilder.RegisterModule<FinalPriceCalculatorModule>();
            containerBuilder.RegisterModule<CaptchaValidatorModule>();
            containerBuilder.RegisterType<WebForMapper>().As<IWebForMapper>();

            containerBuilder.Populate(services);
            var container = containerBuilder.Build();
            return container.Resolve<IServiceProvider>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory, WebForDbContextSeedData seeder)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            if (env.IsDevelopment())
            {
                app.UseBrowserLink();
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }

            else
            {
                app.UseExceptionHandler("/Error/Status/{0}");
            }

            app.UseStatusCodePagesWithRedirects("/Error/Status/{0}");

            app.UseStaticFiles();

            app.UseIdentity();

            #region External Logins Setup

            var googleOption = new GoogleOptions
            {
                ClientId = Configuration["OAuth:Google:ClientId"],
                ClientSecret = Configuration["OAuth:Google:ClientSecret"],
                Events = new OAuthEvents()
                {
                    OnRemoteFailure = ctx =>

                    {
                        ctx.Response.Redirect("/error?ErrorMessage=" + UrlEncoder.Default.Encode(ctx.Failure.Message));
                        ctx.HandleResponse();
                        return Task.FromResult(0);
                    }
                }
            };

            var faceBookOption = new FacebookOptions
            {
                AppId = Configuration["OAuth:Facebook:AppId"],
                AppSecret = Configuration["OAuth:Facebook:AppSecret"],
                //Scope.Add("email"),
                //Scope = new List<string> { "slkjdf"},
                //Scope.Add("email"),
                BackchannelHttpHandler = new FacebookBackChannelHandler(),
                UserInformationEndpoint = "https://graph.facebook.com/v2.4/me?fields=id,name,email,first_name,last_name,location"
            };

            var twitterOption = new TwitterOptions
            {
                ConsumerKey = Configuration["OAuth:Twitter:ConsumerKey"],
                ConsumerSecret = Configuration["OAuth:Twitter:ConsumerSecret"],
                DisplayName = "WebFor Twitter Auth"
            };

            var microsoftAccountOptions = new MicrosoftAccountOptions
            {
                ClientId = Configuration["OAuth:Microsoft:ClientId"],
                ClientSecret = Configuration["OAuth:Microsoft:ClientSecret"],
                //Scope.Add("wl.emails, wl.basic"),
                DisplayName = "WebFor Microsoft OAuth"
            };

            app.UseGoogleAuthentication(googleOption);
            app.UseFacebookAuthentication(faceBookOption);
            app.UseTwitterAuthentication(twitterOption);
            app.UseMicrosoftAccountAuthentication(microsoftAccountOptions);

            #endregion

            app.UseSession();

            app.UseMvc(routes =>
            {
                routes.MapRoute(name: "AreaRoute",
                template: "{area:exists}/{controller}/{action}/{id?}/{title?}",
                defaults: new { controller = "Home", action = "Index" });

                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}/{title?}");


            });

            seeder.SeedAdminUser();
        }
    }
}