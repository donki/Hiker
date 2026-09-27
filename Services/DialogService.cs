namespace Hiker.Services
{
    public interface IDialogService
    {
        Task<bool> ShowConfirmationDialog(string title, string message);
    }

    public class DialogService : IDialogService
    {
        private readonly TranslationService _translation;

        public DialogService(TranslationService translation) => _translation = translation;

        public async Task<bool> ShowConfirmationDialog(string title, string message)
        {
            return await SocShared.ModernDialog.AlertAsync(Application.Current.MainPage, title, message,
                _translation.Translate("Sí"), _translation.Translate("No"));
        }
    }

}
