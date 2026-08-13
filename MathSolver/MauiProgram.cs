using CommunityToolkit.Maui;
using MathSolver.Controls;
using MathSolver.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;

namespace MathSolver
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            ButtonHandler.Mapper.AppendToMapping(
                "InteractivePressAnimation",
                static (_, view) =>
                {
                    if (view is Button button)
                    {
                        InteractiveButtonAnimation.Attach(button);
                    }
                });

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
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
