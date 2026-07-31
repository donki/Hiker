namespace Hiker.Services
{
    public interface IDialogService
    {
        Task<bool> ShowConfirmationDialog(string title, string message);
    }

    public class DialogService : IDialogService
    {
        public async Task<bool> ShowConfirmationDialog(string title, string message)
        {
            return await SocShared.ModernDialog.AlertAsync(Application.Current.MainPage, title, message, "Sí", "No");
        }
    }

}
