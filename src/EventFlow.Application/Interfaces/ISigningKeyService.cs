using System;
using System.Collections.Generic;
using System.Text;

namespace EventFlow.Application.Interfaces;

public interface ISigningKeyService
{
    string GenerateSigningKey();

    string Encrypt(string signingKey);

    string Decrypt(string encryptedSigningKey);
}

