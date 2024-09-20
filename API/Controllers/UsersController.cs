using System.Security.Claims;
using API.Data;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Helpers;
using API.Interfaces;
using AutoMapper;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers
{

    [Authorize]
    public class UsersController : BaseApiController
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;
        private readonly IPhotoService _photoService;

        public UsersController(IUnitOfWork uow, IMapper mapper, IPhotoService photoService)
        {
            _uow = uow;
            _mapper = mapper;
            _photoService = photoService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedList<MemberDto>>> GetUsers([FromQuery]UserParams userParams)
        {
            var gender = await _uow.UserRepository.GetUserGender(User.GetUsername());
            userParams.CurrentUsername = User.GetUsername();

            if (string.IsNullOrEmpty(userParams.Gender))
            {
                userParams.Gender = gender == "male" ? "female" : "male";
            }

            var users = await _uow.UserRepository.GetMembersAsync(userParams);

            Response.AddPaginationHeader(new PaginationHeader(users.CurrentPage, users.PageSize, 
                users.TotalCount, users.TotalPages));

            return Ok(users);
        }

        [HttpGet("{username}")]
        public async Task<ActionResult<MemberDto>> GetUser(string username)
        {
            var currentUser = User.GetUsername();

            return await _uow.UserRepository.GetMemberAsync(username, currentUser == username);
        }

        [HttpPut]        
        public async Task<ActionResult> UpdateUser(MemberUpdateDto memberUpdateDto)
        {
            var user = await _uow.UserRepository.GetUserByUsernameAsync(User.GetUsername());

            if (user == null) return NotFound();

            _mapper.Map(memberUpdateDto, user);

            if (await _uow.Complete()) return NoContent();

            return BadRequest("Failed to update user");
        }

        [HttpPost("add-photo")]
        public async Task<ActionResult<PhotoDto>> AddPhoto(IFormFile file)
        {
            // Get the user from the database
            var user = await _uow.UserRepository.GetUserByUsernameAsync(User.GetUsername());

            // If the user doesn't exist, return an error message
            if (user == null) return NotFound();

            // Call the AddPhotoAsync method in the PhotoService class to upload the photo to Cloudinary
            var result = await _photoService.AddPhotoAsync(file);

            // If the upload was successful, create a new Photo object and set the Url and PublicId properties
            if (result.Error != null) return BadRequest(result.Error.Message);
            
            var photo = new Photo
            {
                Url = result.SecureUrl.AbsoluteUri,
                PublicId = result.PublicId,
                IsApproved = false
            };

            // Add the photo to the user's collection of photos
            user.Photos.Add(photo);

            // Save the changes to the database
            if (await _uow.Complete())
            {
                // Return the photo object
                return CreatedAtAction(nameof(GetUser), 
                    new {username = user.UserName}, _mapper.Map<PhotoDto>(photo));
            }

             // If the upload was not successful, return an error message
            return BadRequest("Problem adding photo");
        }

        [HttpPut("set-main-photo/{photoId}")]
        public async Task<ActionResult> SetMainPhoto(int photoId)
        {
            // Get the user from the database
            var user = await _uow.UserRepository.GetUserByUsernameAsync(User.GetUsername());

            // If the user doesn't exist, return an error message
            if (user == null) return NotFound();

            // Get the photo from the user's collection of photos
            var photo = user.Photos.FirstOrDefault(x => x.Id == photoId);

            // If the photo doesn't exist, return an error message
            if (photo == null) return NotFound();

            // If the photo is already the main photo, return an error message
            if (photo.IsMain) return BadRequest("This is already your main photo");

            // Get the current main photo
            var currentMain = user.Photos.FirstOrDefault(x => x.IsMain);

            // If the current main photo exists, set the IsMain property to false
            if (currentMain != null) currentMain.IsMain = false;

            // Set the IsMain property of the new photo to true
            photo.IsMain = true;

            // Save the changes to the database
            if (await _uow.Complete()) return NoContent();

            // If the upload was not successful, return an error message
            return BadRequest("Failed to set main photo");
        }

        [HttpDelete("delete-photo/{photoId}")]
        public async Task<ActionResult> DeletePhoto(int photoId)
        {
            // Get the user from the database
            var user = await _uow.UserRepository.GetUserByUsernameAsync(User.GetUsername());

            // If the user doesn't exist, return an error message
            if (user == null) return NotFound();

            // Get the photo by id
            var photo = await _uow.PhotoRepository.GetPhotoByIdAsync(photoId);

            // If the photo doesn't exist, return an error message
            if (photo == null) return NotFound();

            // If the photo is the main photo, return an error message
            if (photo.IsMain) return BadRequest("You cannot delete your main photo");

            // If the photo has a public id, call the DeletePhotoAsync method in the PhotoService class to delete the photo from Cloudinary
            if (photo.PublicId != null)
            {
                var result = await _photoService.DeletePhotoAsync(photo.PublicId);

                // If the deletion was not successful, return an error message
                if (result.Error != null) return BadRequest(result.Error.Message);
            }

            // Remove the photo from the user's collection of photos
            user.Photos.Remove(photo);

            // Save the changes to the database
            if (await _uow.Complete()) return Ok();

            // If the upload was not successful, return an error message
            return BadRequest("Failed to delete photo");
        }

    }
}
