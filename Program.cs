using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using QuanLyThuVien.Data;
using QuanLyThuVien.Services;
using QuanLyThuVien.Hubs; // ✅ Thêm để dùng ChatHub
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// ✅ Đăng ký các service
builder.Services.AddSingleton<OnnxImageService>();

// Kết nối Database
builder.Services.AddDbContext<LibraryDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Thêm IHttpContextAccessor
builder.Services.AddHttpContextAccessor();

// ✅ Xác thực bằng Cookie
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
    });

// ✅ Phân quyền
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

// ✅ Cấu hình gửi Email qua Gmail
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<GmailEmailService>();

// ✅ Thêm background service kiểm tra quá hạn
builder.Services.AddHostedService<OverdueCheckService>();

// ✅ Thêm SignalR cho chat realtime
builder.Services.AddSignalR();

// ✅ Thêm MVC
builder.Services.AddControllersWithViews();

var app = builder.Build();

// --- PHẦN KHỞI TẠO TÀI KHOẢN ADMIN ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<LibraryDbContext>();
        context.Database.EnsureCreated();
        DbInitializer.SeedAdminUser(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}
// --- KẾT THÚC PHẦN ADMIN ---

// ✅ Cấu hình pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

// ✅ Cấu hình static files cho ảnh
app.UseStaticFiles(); // Mặc định cho wwwroot
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "wwwroot/BookImages")),
    RequestPath = "/BookImages"
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// ✅ Map route MVC
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ✅ Map SignalR Hub (chat realtime)
app.MapHub<ChatHub>("/chathub");

app.Run();
