namespace YamStudio.Core;

/// <summary>
/// Everything the screens need. PC1 uses LocalStudio (SQLite + disk), PC2 uses RemoteStudio (same calls over the studio LAN),
/// so every screen works identically on both computers.
/// </summary>
public interface IStudio
{
    bool IsHost { get; }
    Task<List<Client>> GetClientsAsync();
    Task<int> SaveClientAsync(Client c);
    Task DeleteClientAsync(int id);
    Task SetStatusAsync(int id, string status);
    Task AddPaymentAsync(int id, decimal amount, string dateIso);
    Task<List<PaymentRow>> GetPaymentsAsync(string fromIso, string toIso);

    Task<List<Photo>> GetPhotosAsync(int clientId);
    Task<byte[]> GetImageAsync(int photoId, bool thumb);
    Task SetSelectedAsync(int photoId, bool selected);
    /// <summary>Creates/fills the client folder (Selected copy), sets status to selected, returns the folder name.</summary>
    Task<string> ConfirmSelectionAsync(int clientId);

    /// <summary>PC2: copies the selected photos to a local folder for editing. PC1: just returns the Selected folder path.</summary>
    Task<string> FetchSelectedAsync(int clientId, string localWorkDir);
    /// <summary>PC2: uploads finished files into the client's Final folder on PC1.</summary>
    Task UploadFinalAsync(int clientId, IEnumerable<string> files);
    /// <summary>PC1 only: path of the client's folder on this computer, or "" if not available.</summary>
    string ClientFolderPath(Client c);
}
