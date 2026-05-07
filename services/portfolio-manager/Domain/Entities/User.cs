namespace PortfolioManager.Domain.Entities;

using System;

/// <summary>
/// Kullanıcı entitysi - Sisteme kayıtlı kullanıcıları temsil eder.
/// </summary>
public class User
{
    public Guid Id { get; set; }
    
    public string Username { get; set; } = string.Empty;
    
    public string Email { get; set; } = string.Empty;
    
    public bool EmailVerified { get; set; }
    
    public string PasswordHash { get; set; } = string.Empty;
    
    public string? FullName { get; set; }
    
    public string Status { get; set; } = "active";
    
    public DateTime? LastLoginAt { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }
}