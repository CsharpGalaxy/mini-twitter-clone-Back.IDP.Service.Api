using Keycloak.AuthServices.Authentication;
using Keycloak.AuthServices.Authorization;
using keycloak_sample.Services;
using keycloak_sample.Services.Abstract;
using keycloak_sample.Services.Concrete;
using keycloak_sample.Services.ServiceOptions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCors();
builder.Services.AddSwaggerGen(setup =>
{
    var jwtSecuritySheme = new OpenApiSecurityScheme
    {
        BearerFormat = "JWT",
        Name = "JWT Authentication",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = JwtBearerDefaults.AuthenticationScheme,
        Description = "Put **_ONLY_** yourt JWT Bearer token on textbox below!",

        Reference = new OpenApiReference
        {
            Id = JwtBearerDefaults.AuthenticationScheme,
            Type = ReferenceType.SecurityScheme
        }
    };

    setup.AddSecurityDefinition(jwtSecuritySheme.Reference.Id, jwtSecuritySheme);

    setup.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtSecuritySheme, Array.Empty<string>() }
    });
});

builder.Services.Configure<KeycloakConfiguration>(builder.Configuration.GetSection("KeycloakConfiguration"));

builder.Services.AddScoped<IUserRolesService, UserRolesManager>();
builder.Services.AddScoped<IKeycloakService, KeycloakManager>();
builder.Services.AddScoped<IUserService, UserManager>();
builder.Services.AddScoped<IRoleService, RoleManager>();
builder.Services.AddKeycloakWebApiAuthentication(builder.Configuration);
builder.Services.AddAuthorization(options =>
{
    var policies = new Dictionary<string, string[]>
    {
        {"UserGetAll", new []{"Admin","User"}},
        { "UserCreate", new []{"Admin","User"}},
        { "UserUpdate", new []{"Admin","User"}},
        {"UserDelete", new []{"Admin","User"}}
    };

    foreach (var policy in policies)
    {
        options.AddPolicy(policy.Key, builder =>
        {
            builder.RequireResourceRoles(policy.Value);
        });
    }
}).AddKeycloakAuthorization(builder.Configuration);
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
  
}
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
    c.RoutePrefix = string.Empty; // نمایش در ریشه سایت
});


app.UseHttpsRedirection();
app.UseCors(x => x.AllowAnyHeader().AllowAnyOrigin().AllowAnyMethod());
app.UseAuthentication();
app.UseAuthorization();


app.MapControllers();

app.Run();
