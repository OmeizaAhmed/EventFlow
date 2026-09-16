namespace EventFlow.Application.DTOs;
using System.ComponentModel.DataAnnotations;
public record RegisterInput(string Email, string Password, string FirstName, string LastName);

public record LoginInput(string Email, string Password);