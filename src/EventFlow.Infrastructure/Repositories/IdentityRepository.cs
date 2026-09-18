namespace EventFlow.Infrastructure.Repositories;
using EventFlow.Application.Interfaces;
using Microsoft.AspNetCore.Identity;
using EventFlow.Infrastructure.Identity;

public class IdentityRepository : IIdentityRepository
{
    private readonly UserManager<ApplicationUser> _userManager; 
    private readonly RoleManager<IdentityRole> _roleManager;
    public IdentityRepository(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<Guid> CreateIdentityAsync(string email, string password)
    {
        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));
        }
        return user.Id;
    }

    public async Task<bool> CheckPasswordAsync(string username, string password)
    {
        var user = await _userManager.FindByNameAsync(username);
        if (user == null)
        {
            return false;
        }
        return await _userManager.CheckPasswordAsync(user, password);
    }

    public async Task DeleteIdentityAsync(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user != null)
        {
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    public async Task UpdateIdentityEmailAsync(Guid id, string email)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user != null)
        {
            user.Email = email;
            user.UserName = email;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    public async Task CreateRoleAsync(string roleName)
    {
        var role = new IdentityRole(roleName);
        var result = await _roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    public async Task DeleteRoleAsync(string roleName)
    {
        var role = await _roleManager.FindByNameAsync(roleName);
        if (role != null)
        {
            var result = await _roleManager.DeleteAsync(role);
            if (!result.Succeeded)
            {
                throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    public async Task<ICollection<string>> GetIdentityRolesAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new List<string>();
        }
        var roles = await _userManager.GetRolesAsync(user);
        return roles;
    }

    
}