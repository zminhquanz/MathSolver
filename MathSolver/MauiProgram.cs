using MathSolver.Services;
using Microsoft.Extensions.Logging;

namespace MathSolver
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    // Toàn bộ font được quản lý tại AppFontCatalog.
                    // Khi thêm font mới, không cần sửa MauiProgram.
                    AppFontCatalog.RegisterFonts(fonts);
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
