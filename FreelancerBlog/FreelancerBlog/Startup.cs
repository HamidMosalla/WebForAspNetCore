using System;
using System.Globalization;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Autofac;
using AutoMapper;
using FreelancerBlog.Core.DomainModels;
using FreelancerBlog.Core.Services.Shared;
using FreelancerBlog.Core.Wrappers;
using FreelancerBlog.Data.EntityFramework;
using FreelancerBlog.Infrastructure.DependencyInjection;
using FreelancerBlog.Services.Shared;
using FreelancerBlog.Services.Wrappers;
using FreelancerBlog.Web.Experimental;
using MediatR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.Twitter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FreelancerBlog.Web
{
    public class Startup
    {
        public Startup(IWebHostEnvironment env)
        {
            // Set up configuration sources.
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            if (env.IsDevelopment())
            {
                // For more details on using the user secret store see http://go.microsoft.com/fwlink/?LinkID=532709
                builder.AddUserSecrets("aspnet5-freelancerblog-23975498-e4cd-4072-bc80-0fca99fd4a83");
            }

            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<FreelancerBlogContext>(options => options.UseSqlServer(Configuration["Data:DefaultConnection:ConnectionString"]));

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConfiguration(Configuration.GetSection("Logging"));
                loggingBuilder.AddConsole();
                loggingBuilder.AddDebug();
            });

            // find out abo8ut it
            // AutoValidateAntiforgeryTokenAttribute
            // IDataProtectionProvider
            // IDataProtector

            services.AddIdentity<ApplicationUser, IdentityRole>(o =>
                {
                    o.Password.RequireDigit = false;
                    o.Password.RequireLowercase = false;
                    o.Password.RequireNonAlphanumeric = false;
                    o.Password.RequireUppercase = false;
                    o.Password.RequiredLength = 6;
                })
                .AddEntityFrameworkStores<FreelancerBlogContext>()
                .AddDefaultTokenProviders();

            services.ConfigureApplicationCookie(options => options.LoginPath = "/Account/LogIn");

            services.AddLocalization(options => options.ResourcesPath = "Resources");

            services.AddControllersWithViews(options =>
                {
                    options.EnableEndpointRouting = false;
                })
                .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
                .AddDataAnnotationsLocalization();

            services.AddMvcJQueryDataTables();

            services.AddAutoMapper();

            services.AddMemoryCache();

            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.Name = ".FreelancerBlog";
            });

            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                    .AddCookie(o =>
                    {
                        o.LoginPath = new PathString("/Account/Login/");
                        o.AccessDeniedPath = new PathString("/Account/Forbidden/");
                    })
                    .AddFacebook(o =>
                    {
                        o.AppId = Configuration["OAuth:Facebook:AppId"];
                        o.AppSecret = Configuration["OAuth:Facebook:AppSecret"];
                        //Scope.Add("email"),
                        //Scope = new List<string> { "slkjdf"},
                        //Scope.Add("email"),
                        o.BackchannelHttpHandler = new FacebookBackChannelHandler();
                        o.UserInformationEndpoint = "https://graph.facebook.com/v2.4/me?fields=id,name,email,first_name,last_name,location";
                    })
                    .AddGoogle(o =>
                    {
                        o.ClientId = Configuration["OAuth:Google:ClientId"];
                        o.ClientSecret = Configuration["OAuth:Google:ClientSecret"];
                        o.Events = new OAuthEvents()
                        {
                            OnRemoteFailure = ctx =>
                            {
                                ctx.Response.Redirect("/error?ErrorMessage=" +
                                                      UrlEncoder.Default.Encode(ctx.Failure.Message));
                                ctx.HandleResponse();
                                return Task.FromResult(0);
                            }
                        };
                    })
                    .AddMicrosoftAccount(MicrosoftAccountDefaults.AuthenticationScheme, "FreelancerBlog Microsoft OAuth", o =>
                    {
                        o.ClientId = Configuration["OAuth:Microsoft:ClientId"];
                        o.ClientSecret = Configuration["OAuth:Microsoft:ClientSecret"];
                        //Scope.Add("wl.emails, wl.basic"),
                    })
                    .AddTwitter(TwitterDefaults.AuthenticationScheme, "FreelancerBlog Twitter Auth", o =>
                    {
                        o.ConsumerKey = Configuration["OAuth:Twitter:ConsumerKey"];
                        o.ConsumerSecret = Configuration["OAuth:Twitter:ConsumerSecret"];
                    });

            //new CookieAuthenticationOptions()
            //{
            //    AuthenticationScheme = "FreelancerBlogCookieMiddlewareInstance",
            //    LoginPath = new PathString("/Account/Login/"),
            //    AccessDeniedPath = new PathString("/Account/Forbidden/"),
            //    AutomaticAuthenticate = true,
            //    AutomaticChallenge = true
            //}

            services.Configure<AuthMessageSenderSecrets>(Configuration.GetSection("AuthMessageSenderSecrets"));

            services.AddTransient<IUrlHelperFactory, UrlHelperFactory>();

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton<IActionContextAccessor, ActionContextAccessor>();
            services.AddTransient<HttpClient>();
            services.AddSingleton<IConfiguration>(Configuration);
            services.AddSingleton<IConfigurationBinderWrapper, ConfigurationBinderWrapper>();
            services.AddSingleton<ILoggerFactoryWrapper, LoggerFactoryWrapper>();
            services.AddScoped<IRazorViewToString, RazorViewToString>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory, FreelancerBlogContextSeedData seeder)
        {
            if (env.IsDevelopment())
            {
                app.UseAspNetCoreExceptionHandler();
            }
            else
            {
                app.UseAspNetCoreExceptionHandler();
                app.UseStatusCodePagesWithRedirects("/Error/Status/{0}");
            }

            app.UseStaticFiles();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseSession();

            var supportedCultures = new[]
            {
                new CultureInfo("fa-IR"),
                new CultureInfo("en-US")
            };

            app.UseRequestLocalization(new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture(culture: "en-US", uiCulture: "en-US"),
                // Formatting numbers, dates, etc.
                SupportedCultures = supportedCultures,
                // UI strings that we have localized.
                SupportedUICultures = supportedCultures
            });

            app.UseMvcJQueryDataTables();

            app.UseMvc(routes =>
            {
                routes.MapRoute("AreaRoute", "{area:exists}/{controller=Home}/{action=Index}/{id?}/{title?}");
                routes.MapRoute("default", "{controller=Home}/{action=Index}/{id?}/{title?}");
            });

            //app.UseEndpoints(endpoints =>
            //{
            //    endpoints.MapHub<ChatHub>("/chat");
            //    endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            //});

            seeder.SeedAdminUser().GetAwaiter().GetResult();
        }

        public void ConfigureContainer(ContainerBuilder builder)
        {
            builder.RegisterType<Mediator>().As<IMediator>().InstancePerLifetimeScope();

            builder.Register<ServiceFactory>(ctx =>
            {
                var c = ctx.Resolve<IComponentContext>();
                return t => c.TryResolve(t, out var o) ? o : null;
            }).InstancePerLifetimeScope();

            var dataAssembly = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName("FreelancerBlog.Data"));
            var servicesAssembly = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName("FreelancerBlog.Services"));
            builder.RegisterAssemblyTypes(dataAssembly, servicesAssembly, Assembly.GetEntryAssembly()).AsImplementedInterfaces();

            builder.RegisterModule<AuthMessageSenderModule>();
            builder.RegisterModule<FreelancerBlogDbContextSeedDataModule>();
            builder.RegisterModule<FileManagerModule>();
            builder.RegisterModule<FileSystemWrapperModule>();
        }
    }
}