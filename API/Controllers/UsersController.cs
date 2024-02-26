using System.Security.Claims;
using API.Data;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Helpers;
using API.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers
{

    [Authorize]
    public class UsersController : BaseApiController
    {
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;
        private readonly IPhotoService _photoService;

        public UsersController(IUserRepository userRepository, IMapper mapper, IPhotoService photoService)
        {
            _mapper = mapper;
            _photoService = photoService;
            _userRepository = userRepository;
        }

        [HttpGet]
        public async Task<ActionResult<PagedList<MemberDto>>> GetUsers([FromQuery]UserParams userParams)
        {
            var currentUser = await _userRepository.GetUserByUsernameAsync(User.GetUsername());
            userParams.CurrentUsername = currentUser.UserName;

            if (string.IsNullOrEmpty(userParams.Gender))
            {
                userParams.Gender = currentUser.Gender == "male" ? "female" : "male";
            }

            var users = await _userRepository.GetMembersAsync(userParams);

            Response.AddPaginationHeader(new PaginationHeader(users.CurrentPage, users.PageSize, 
                users.TotalCount, users.TotalPages));

            return Ok(users);
        }

        [HttpGet("{username}")]
        public async Task<ActionResult<MemberDto>> GetUser(string username)
        {
            return await _userRepository.GetMemberAsync(username);
        }

        [HttpPut]        
        public async Task<ActionResult> UpdateUser(MemberUpdateDto memberUpdateDto)
        {
            var user = await _userRepository.GetUserByUsernameAsync(User.GetUsername());

            if (user == null) return NotFound();

            _mapper.Map(memberUpdateDto, user);

            if (await _userRepository.SaveAsync()) return NoContent();

            return BadRequest("Failed to update user");
        }

        [HttpPost("add-photo")]
        public async Task<ActionResult<PhotoDto>> AddPhoto(IFormFile file)
        {
            // Get the user from the database
            var user = await _userRepository.GetUserByUsernameAsync(User.GetUsername());

            // If the user doesn't exist, return an error message
            if (user == null) return NotFound();

            // Call the AddPhotoAsync method in the PhotoService class to upload the photo to Cloudinary
            var result = await _photoService.AddPhotoAsync(file);

            // If the upload was successful, create a new Photo object and set the Url and PublicId properties
            if (result.Error != null) return BadRequest(result.Error.Message);
            
            var photo = new Photo
            {
                Url = result.SecureUrl.AbsoluteUri,
                PublicId = result.PublicId
            };

            // If the user doesn't have a main photo, set the new photo as the main photo
            if (user.Photos.Count == 0)
            {
                photo.IsMain = true;
            }

            // Add the photo to the user's collection of photos
            user.Photos.Add(photo);

            // Save the changes to the database
            if (await _userRepository.SaveAsync())
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
            var user = await _userRepository.GetUserByUsernameAsync(User.GetUsername());

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
            if (await _userRepository.SaveAsync()) return NoContent();

            // If the upload was not successful, return an error message
            return BadRequest("Failed to set main photo");
        }

        [HttpDelete("delete-photo/{photoId}")]
        public async Task<ActionResult> DeletePhoto(int photoId)
        {
            // Get the user from the database
            var user = await _userRepository.GetUserByUsernameAsync(User.GetUsername());

            // If the user doesn't exist, return an error message
            if (user == null) return NotFound();

            // Get the photo from the user's collection of photos
            var photo = user.Photos.FirstOrDefault(x => x.Id == photoId);

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
            if (await _userRepository.SaveAsync()) return Ok();

            // If the upload was not successful, return an error message
            return BadRequest("Failed to delete photo");
        }

    }
}
