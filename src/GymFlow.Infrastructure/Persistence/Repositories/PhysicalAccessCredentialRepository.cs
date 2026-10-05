using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Infrastructure.Persistence.Repositories;

public class PhysicalAccessCredentialRepository
    : IPhysicalAccessCredentialRepository
{
    private readonly AppDbContext _context;

    public PhysicalAccessCredentialRepository(
        AppDbContext context)
    {
        _context = context;
    }

    public async Task<PhysicalAccessCredential?>
        GetByExternalIdentifierAsync(
            Guid gymId,
            string providerKey,
            PhysicalAccessCredentialType type,
            string externalIdentifier)
    {
        return await _context.PhysicalAccessCredentials
            .Include(credential => credential.Student)
                .ThenInclude(student => student.User)
            .FirstOrDefaultAsync(credential =>
                credential.GymId == gymId &&
                credential.Student.User.GymId == gymId &&
                credential.ProviderKey == providerKey &&
                credential.Type == type &&
                credential.ExternalIdentifier ==
                    externalIdentifier);
    }

    public async Task<PhysicalAccessCredential?>
        GetByIdAsync(
            Guid credentialId,
            Guid gymId)
    {
        return await _context.PhysicalAccessCredentials
            .Include(credential => credential.Student)
                .ThenInclude(student => student.User)
            .FirstOrDefaultAsync(credential =>
                credential.Id == credentialId &&
                credential.GymId == gymId &&
                credential.Student.User.GymId == gymId);
    }

    public async Task<List<PhysicalAccessCredential>>
        GetByStudentAsync(
            Guid studentId,
            Guid gymId)
    {
        return await _context.PhysicalAccessCredentials
            .AsNoTracking()
            .Include(credential => credential.Student)
                .ThenInclude(student => student.User)
            .Where(credential =>
                credential.StudentId == studentId &&
                credential.GymId == gymId &&
                credential.Student.User.GymId == gymId)
            .OrderBy(credential => credential.ProviderKey)
            .ThenBy(credential => credential.Type)
            .ToListAsync();
    }

    public async Task AddAsync(
        PhysicalAccessCredential credential)
    {
        await _context.PhysicalAccessCredentials
            .AddAsync(credential);

        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(
        PhysicalAccessCredential credential)
    {
        _context.PhysicalAccessCredentials
            .Update(credential);

        await _context.SaveChangesAsync();
    }
}