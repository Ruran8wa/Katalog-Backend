using Microsoft.AspNetCore.Http;

namespace Katalog_Backend.Services.Interfaces;

public record StorageUploadResult(string Url, string PublicId);

public interface IFileStorageService
{
    Task<StorageUploadResult> UploadImageAsync(IFormFile file, string folder = "variants");
    Task<bool> DeleteImageAsync(string publicId);
}
