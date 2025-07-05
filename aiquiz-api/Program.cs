using aiquiz_api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddSingleton<QuizManager>();
builder.Services.AddCors();

// Add this line to enable Kestrel to use configuration
builder.WebHost.ConfigureKestrel(options =>
{
    options.Configure(builder.Configuration.GetSection("Kestrel"));
});
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();
app.UseCors(builder =>
    builder
      //.WithOrigins("http://localhost:3000")
      .SetIsOriginAllowed(_ => true) // Allow any origin
      .AllowAnyHeader()
      .AllowAnyMethod()
      .AllowCredentials()
);
app.UseAuthorization();

app.MapControllers();
app.MapHub<aiquiz_api.Hubs.QuizHub>("/quizhub");

app.Run();
