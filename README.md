# Example C# Hybrid Online/Offline Licensing

This is an example of how to set up a hybrid online/offline licensing system with Keygen.
The online portion will validate and activate a license key, and pull in its entitlements.
The offline portion will checkout, verify, and decrypt a cryptographic license file
including the same data.

The example is written in C# and .NET, using Ed25519 and AES-256-GCM.

## Running the example

First, install dependencies with [`dotnet`](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet):

```
dotnet restore
```

Then run the program:

```
dotnet run
```

You should see log output indicating the current license has been validated, activated, and
that a license file has been checked out, verified, and decrypted.

```
license=5383bb59-82de-4686-9680-9d156310779b
Validating license...
validation=NO_MACHINE
Activating license...
machine=7cf22490-49a4-4d3f-ae07-bd3dc330aad1
entitlements=OFFLINE_SUPPORT
Offline support is enabled!
Checking out offline license file...
License file is valid!
license=5383bb59-82de-4686-9680-9d156310779b
entitlements=OFFLINE_SUPPORT
Deactivating license...
```

## Questions?

Reach out at [support@keygen.sh](mailto:support@keygen.sh) if you have any
questions or concerns!
