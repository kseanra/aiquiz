using aiquiz_api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddSingleton<IQuizManager, QuizManager>();
builder.Services.AddSingleton<IRoomManager, RoomManager>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowQuizClient", builder =>
    {
        // Allow any origin, header, and method
        builder
            .WithOrigins("http://192.168.68.59:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
builder.Services.AddSignalR();

// Add this line to enable Kestrel to use configuration
// builder.WebHost.ConfigureKestrel(options =>
// {
//     options.Configure(builder.Configuration.GetSection("Kestrel"));
// });
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();
app.UseCors("AllowQuizClient");

app.UseAuthorization();

app.MapControllers();
app.MapHub<aiquiz_api.Hubs.QuizHub>("/quizhub");

app.Run();
