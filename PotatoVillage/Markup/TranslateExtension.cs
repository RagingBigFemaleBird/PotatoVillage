using Microsoft.Maui.Controls.Xaml;
using PotatoVillage.Services;

namespace PotatoVillage.Markup
{
    [ContentProperty(nameof(Key))]
    public class TranslateExtension : IMarkupExtension<string>
    {
        public string Key { get; set; } = string.Empty;

        public string ProvideValue(IServiceProvider serviceProvider)
        {
            return LocalizationManager.Instance.GetString(Key, Key);
        }

        object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider)
        {
            return ProvideValue(serviceProvider);
        }
    }
}
