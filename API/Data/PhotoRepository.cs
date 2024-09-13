using System;
using API.DTOs;
using API.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace API.Data
{
    public class PhotoRepository : IPhotoRepository
    {
        private readonly DataContext _context;

        public PhotoRepository(DataContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<PhotoForApprovalDto>> GetUnapprovedPhotosAsync()
        {
            return await _context.Photos
                .IgnoreQueryFilters()
                .Where(p => !p.IsApproved)
                .Select(p => new PhotoForApprovalDto
                {
                    Id = p.Id,
                    Url = p.Url,
                    Username = p.AppUser.UserName,
                    IsApproved = p.IsApproved
                }).ToListAsync();
                
        }
    }
}
