namespace YamStudio.Core;

/// <summary>Unified studio interface — works identically on every installation.</summary>
public interface IStudio
{
    Task<List<Client>> GetClientsAsync();
    Task<int> SaveClientAsync(Client c);
    Task DeleteClientAsync(int id);
    Task SetStatusAsync(int id, string status);
    Task AddPaymentAsync(int id, decimal amount, string dateIso);
    Task<List<PaymentRow>> GetPaymentsAsync(string fromIso, string toIso);

    Task<List<Photo>> GetPhotosAsync(int clientId);
    Task<byte[]> GetImageAsync(int photoId, bool thumb);
    Task SetSelectedAsync(int photoId, bool selected);
    Task<string> ConfirmSelectionAsync(int clientId);

    Task UploadFinalAsync(int clientId, IEnumerable<string> files);
    string ClientFolderPath(Client c);
}
