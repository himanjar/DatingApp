using API.Helpers;
using API.Interfaces;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;

namespace API.Services
{
    public class PhotoService : IPhotoService
    {
        private readonly Cloudinary _cloudinary;
        public PhotoService(IOptions<CloudinarySettings> config)
        {
            // Create a new Cloudinary account and add the credentials to the user-secrets file
            // The CloudinarySettings class is defined in the Helpers folder
            var acc = new Account(
                config.Value.CloudName,
                config.Value.ApiKey,
                config.Value.ApiSecret
            );

            // Create a new Cloudinary instance and pass in the account details
            _cloudinary = new Cloudinary(acc);   
        }
        public async Task<ImageUploadResult> AddPhotoAsync(IFormFile file)
        {
            var uploadResult = new ImageUploadResult();

            if (file.Length > 0)
            {
                using var stream = file.OpenReadStream(); // Open a stream to read the file
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream), // Pass in the file name and the stream
                    Transformation = new Transformation().Height(500).Width(500).Crop("fill").Gravity("face"), // Resize the image to 500x500 and crop it to the face
                    Folder = "da-net7" // Create a folder in Cloudinary to store the images
                };
                uploadResult = await _cloudinary.UploadAsync(uploadParams); // Upload the image to Cloudinary
            }

            return uploadResult;
        }

        public async Task<DeletionResult> DeletePhotoAsync(string publicId)
        {
            var deleteParams = new DeletionParams(publicId); // Pass in the public ID of the image to delete
            return await _cloudinary.DestroyAsync(deleteParams); // Delete the image from Cloudinary
        }
    }
}
