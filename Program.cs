using FluentValidation;
using FluentValidation.AspNetCore;
using FluentValidation.Validators;
using Hangfire;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using src.Core;
using src.Data;
using src.Emails;
using src.Localization.Resources;
using src.Repositories.Applications;
using src.Repositories.Comments;
using src.Repositories.EmailConfigs;
using src.Repositories.Messages;
using src.Repositories.Request;
using src.Repositories.RequestAttachments;
using src.Repositories.Roles;
using src.Repositories.Settings;
using src.Repositories.Users;
using src.Web.Common;
using src.Web.Common.Security;
using src.Web.Common.Validation;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Serialization;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
        });

        builder.Services.AddHangfire(x => x.UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection")));
        builder.Services.AddHangfireServer(options => options.WorkerCount = 1);
        GlobalConfiguration.Configuration.UseSerializerSettings(new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        });
        builder.Services.AddControllersWithViews()
            .SetCompatibilityVersion(CompatibilityVersion.Version_3_0) // Set the appropriate compatibility version
            .AddSessionStateTempDataProvider()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles; // Equivalent to ReferenceLoopHandling.Ignore
            });
        builder.Services.AddControllersWithViews(options =>
        {
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
            //options.InputFormatters.Insert(0, new RawJsonBodyInputFormatter());
        });
        builder.Services.AddAntiforgery(o => o.SuppressXFrameOptionsHeader = true);
        builder.Services.Configure<FormOptions>(options =>
        {
            options.ValueLengthLimit = int.MaxValue;
            options.MultipartBodyLengthLimit = int.MaxValue;
            options.MultipartHeadersLengthLimit = int.MaxValue;
        });
        //Settings
        builder.Services.AddOptions();
        builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));
        builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
        builder.Services.Configure<CCEmailSettings>(builder.Configuration.GetSection("CCEmailSettings"));
        //Memory
        builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
        builder.Services.AddMemoryCache();
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromHours(2);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
        });


        builder.Services.AddControllersWithViews()
            .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
            .AddDataAnnotationsLocalization();


        //Auth

        builder.Services.AddControllersWithViews(options =>
        {
            options.Filters.Add(new AuthorizeFilter(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));
        })
         // Maintain property names during serialization.
         .AddJsonOptions(options =>
         {
             options.JsonSerializerOptions.PropertyNamingPolicy = null; // or new DefaultNamingPolicy();
         });
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        }
        ).AddCookie(
            CookieAuthenticationDefaults.AuthenticationScheme,
            options =>
            {
                options.AccessDeniedPath = "/Common/AccessDenied";
                options.LoginPath = "/Account/Login";
            }
        );

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(Constants.RoleNames.Administrator, policyBuilder =>
            {
                policyBuilder.RequireAuthenticatedUser()
                    .RequireAssertion(context => context.User.HasClaim(ClaimTypes.Role, Constants.RoleNames.Administrator) || context.User.HasClaim(ClaimTypes.Role, Constants.RoleNames.Supporter) || context.User.HasClaim(ClaimTypes.Role, Constants.RoleNames.BO) || context.User.HasClaim(ClaimTypes.Role, Constants.RoleNames.IT))
                    .Build();
            });
            options.AddPolicy(Constants.RoleNames.User, policyBuilder =>
            {
                policyBuilder.RequireAuthenticatedUser()
                    .RequireAssertion(context => context.User.HasClaim(ClaimTypes.Role, Constants.RoleNames.User))
                    .Build();
            });
        });

        //Add Fluent Validation
        builder.Services.AddFluentValidationAutoValidation();
        builder.Services.AddFluentValidationClientsideAdapters();
        builder.Services.AddValidatorsFromAssemblyContaining<ChangeRequestValidator>();

        builder.Services.Configure<FormOptions>(options =>
        {
            options.ValueLengthLimit = int.MaxValue;
            options.MultipartBodyLengthLimit = int.MaxValue;
            options.MultipartHeadersLengthLimit = int.MaxValue;
        });

        // Add services to the container.
        builder.Services.AddControllersWithViews();


        // Add services to the container.
        builder.Services.AddSignalR();
        builder.Services.AddDataProtection();
        builder.Services.AddHttpContextAccessor();

        //Inject services

        builder.Services.AddAntiforgery(o => o.SuppressXFrameOptionsHeader = true);
        builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        builder.Services.AddScoped<IDbContext, AppDbContext>();
        builder.Services.AddScoped<IUserSession, UserSession>();
        builder.Services.AddScoped<ISignInManager, SignInManager>();
        builder.Services.AddScoped<IRoleRepository, RoleRepository>();
        builder.Services.AddTransient<IDateTime, DateTimeAdapter>();
        builder.Services.AddScoped<IUserRepository, UserRepository>();       
        builder.Services.AddScoped<ICommentsRepository, CommentsRepository>();
        builder.Services.AddScoped<IRequestRepository, RequestRepository>();
        builder.Services.AddScoped<IRequestAttachmentsRepository, RequestAttachmentsRepository>();
        builder.Services.AddScoped<IEmailSender, EmailSender>();
        builder.Services.AddScoped<IMessageService, MessageService>();
        builder.Services.AddScoped<ISettingRepository, SettingRepository>();
        builder.Services.AddScoped<IEmailTemplateRepository, EmailTemplateRepository>();
        builder.Services.AddScoped<IApplicationRepository, ApplicationRepository>();
        builder.Services.AddScoped<IEmailConfigRepository, EmailConfigRepository>();

        // Localization
        builder.Services.AddControllersWithViews().AddViewLocalization()
                   .AddDataAnnotationsLocalization(options =>
                   {
                       options.DataAnnotationLocalizerProvider = (type, factory) =>
                           factory.Create(typeof(SharedResource));
                   });


        
        builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

        builder.Services.Configure<RequestLocalizationOptions>(options =>
        {
            var supportedCultures = new[]
            {
        new CultureInfo("vi-VN"),
        new CultureInfo("en-GB")
            };
            options.DefaultRequestCulture = new RequestCulture("vi-VN");
            options.SupportedCultures = supportedCultures;
            options.SupportedUICultures = supportedCultures;
        });


        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        // Load configuration from appsettings.json
        builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        builder.Configuration.AddEnvironmentVariables();

        // Enable HTTPS redirection
        app.UseHttpsRedirection();

        // Serve static files
        app.UseStaticFiles();

        // Configure the request pipeline for  sessions and routing
        app.UseRouting();
        app.UseSession();

        // Enable authorization
        app.UseAuthentication();
        app.UseAuthorization();

        // Hangfire
        app.UseHangfireDashboard();

        // Configure request localization
        app.UseRequestLocalization();

        // Cookie Policy
        app.UseCookiePolicy();

        app.MapControllerRoute(
            name: "areaRoute",
            pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}/{*param}"
        );



        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Account}/{action=Login}/{id?}");

        app.Run();
    }
}