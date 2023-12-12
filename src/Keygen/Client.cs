using DeviceId;
using NSec.Cryptography;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Keygen
{
  public class Client
  {
    private RestClient client = null;
    private string account = null;

    public Client(string account, string key = null)
    {
      this.client = new RestClient($"https://api.keygen.sh/v1/accounts/{account}");
      this.account = account;

      if (key != null)
      {
        client.AddDefaultHeader("Authorization", $"License {key}");
      }
    }

    async public Task<License> Me()
    {
      var request = new RestRequest("me", Method.Get)
        .AddHeader("Accept", "application/json");

      var response = await client.ExecuteAsync<Document<License>>(request);
      if (response.Data.Errors.Count > 0)
      {
        var err = response.Data.Errors[0];

        throw new Exception($"{err.Title}: {err.Detail} ({err.Code})");
      }

      var license = response.Data.Data;
      license.client = this;

      return license;
    }

    async public Task<Validation> Validate(License license)
    {
      var request = new RestRequest($"licenses/{license.ID}/actions/validate", Method.Post)
        .AddHeader("Content-Type", "application/json")
        .AddHeader("Accept", "application/json")
        .AddJsonBody(new
        {
          meta = new
          {
            scope = new
            {
              fingerprint = Fingerprint()
            }
          }
        });

      var response = await client.ExecuteAsync<DocumentWithMeta<License, Validation>>(request);
      if (response.Data.Errors.Count > 0)
      {
        var err = response.Data.Errors[0];

        throw new Exception($"{err.Title}: {err.Detail} ({err.Code})");
      }

      return response.Data.Meta;
    }

    async public Task<Machine> Activate(License license)
    {
      var request = new RestRequest("machines", Method.Post)
        .AddHeader("Content-Type", "application/json")
        .AddHeader("Accept", "application/json")
        .AddJsonBody(new
        {
          data = new
          {
            type = "machine",
            attributes = new
            {
              fingerprint = Fingerprint(),
            },
            relationships = new
            {
              license = new
              {
                data = new
                {
                  type = "license",
                  id = license.ID,
                }
              }
            }
          }
        });

      var response = await client.ExecuteAsync<Document<Machine>>(request);
      if (response.Data.Errors.Count > 0)
      {
        var err = response.Data.Errors[0];

        throw new Exception($"{err.Title}: {err.Detail} ({err.Code})");
      }

      var machine = response.Data.Data;
      machine.client = this;

      return machine;
    }

    async public Task Deactivate(Machine machine)
    {
      var request = new RestRequest($"machines/{machine.ID}", Method.Delete)
        .AddHeader("Accept", "application/json");

      var response = await client.ExecuteAsync<Document<Machine>>(request);
      if (response.StatusCode == HttpStatusCode.NoContent)
      {
        return;
      }

      if (response.Data.Errors.Count > 0)
      {
        var err = response.Data.Errors[0];

        throw new Exception($"{err.Title}: {err.Detail} ({err.Code})");
      }
    }

    async public Task<Checkout> Checkout(License license)
    {
      var request = new RestRequest($"licenses/{license.ID}/actions/check-out", Method.Post)
        .AddHeader("Content-Type", "application/json")
        .AddHeader("Accept", "application/json")
        .AddJsonBody(new
        {
          meta = new
          {
            include = new[] { "entitlements" },
            encrypt = true
          }
        });

      var response = await client.ExecuteAsync<Document<Checkout>>(request);
      if (response.Data.Errors.Count > 0)
      {
        var err = response.Data.Errors[0];

        throw new Exception($"{err.Title}: {err.Detail} ({err.Code})");
      }

      return response.Data.Data;
    }

    async public Task<List<Entitlement>> Entitlements(License license)
    {
      var request = new RestRequest($"licenses/{license.ID}/entitlements", Method.Get)
        .AddHeader("Accept", "application/json");

      var response = await client.ExecuteAsync<Document<List<Entitlement>>>(request);
      if (response.Data.Errors.Count > 0)
      {
        var err = response.Data.Errors[0];

        throw new Exception($"{err.Title}: {err.Detail} ({err.Code})");
      }

      return response.Data.Data;
    }

    private string Fingerprint()
    {
      return account + ":" + new DeviceIdBuilder()
        .OnWindows(windows =>
          windows.AddMotherboardSerialNumber().AddSystemUuid().AddProcessorId()
        )
        .OnLinux(linux =>
          linux.AddMotherboardSerialNumber().AddMachineId().AddCpuInfo()
        )
        .OnMac(mac =>
          mac.AddSystemDriveSerialNumber().AddPlatformSerialNumber()
        )
        .ToString();
    }

    private class Document<T>
    {
      public T Data { get; set; }
      public List<Error> Errors { get; set; } = new();
    }

    private class DocumentWithMeta<T, M> : Document<T>
    {
      public M Meta { get; set; }
    }

    public class Error
    {
      public string Title { get; set; }
      public string Detail { get; set; }
      public string Code { get; set; }
    }
  }

  public class License
  {
    internal Client client;

    public string Type { get; set; }
    public string ID { get; set; }
    public LicenseAttributes Attributes { get; set; }

    async public Task<Validation> Validate()
    {
      return await client.Validate(this);
    }

    async public Task<Machine> Activate()
    {
      return await client.Activate(this);
    }

    async public Task<LicenseFile> Checkout()
    {
      var checkout = await client.Checkout(this);

      return new LicenseFile(certificate: checkout.Attributes.Certificate);
    }

    async public Task<List<Entitlement>> Entitlements()
    {
      return await client.Entitlements(this);
    }

    public class LicenseAttributes
    {
      public string Expiry { get; set; }
      public string Name { get; set; }
      public string Key { get; set; }
    }
  }

  public class Validation
  {
    public string Code { get; set; }
    public bool Valid { get; set; }

    public bool NotActivated
    {
      get
      {
        return Code == "NO_MACHINES" || Code == "NO_MACHINE" || Code == "FINGERPRINT_SCOPE_MISMATCH";
      }
    }
  }

  public class Checkout
  {
    public string Type { get; set; }
    public string ID { get; set; }
    public CheckoutAttributes Attributes { get; set; }

    public class CheckoutAttributes
    {
      public string Certificate { get; set; }
    }
  }

  public class Machine
  {
    internal Client client;

    public string Type { get; set; }
    public string ID { get; set; }
    public MachineAttributes Attributes { get; set; }

    public class MachineAttributes
    {
      public string Fingerprint { get; set; }
      public string Name { get; set; }
    }

    async public Task Deactivate()
    {
      await client.Deactivate(this);
    }
  }

  public class Entitlement
  {
    public string Type { get; set; }
    public string ID { get; set; }
    public EntitlementAttributes Attributes { get; set; }

    public class EntitlementAttributes
    {
      public string Name { get; set; }
      public string Code { get; set; }
    }
  }

  public class LicenseFile
  {
    private SignatureAlgorithm ed25519 = SignatureAlgorithm.Ed25519;
    private Certificate certificate;

    public LicenseFile(string certificate)
    {
      var enc = Regex.Replace(certificate, "(^-----BEGIN LICENSE FILE-----[\\r\\n]+|[\\r\\n]+|-----END LICENSE FILE-----[\\r\\n]+$)", "");
      var dec = Encoding.UTF8.GetString(
        Convert.FromBase64String(enc)
      );

      this.certificate = JsonSerializer.Deserialize<Certificate>(dec, new JsonSerializerOptions
      {
        PropertyNameCaseInsensitive = true
      });
    }

    // Verify verifies the license file's signature using the Ed25519 public key.
    public bool Verify(string key)
    {
      if (certificate.Alg != "aes-256-gcm+ed25519" && certificate.Alg != "base64+ed25519")
      {
        return false;
      }

      var sig = Convert.FromBase64String(certificate.Sig);
      var data = Encoding.UTF8.GetBytes($"license/{certificate.Enc}");
      var pub = PublicKey.Import(ed25519, Convert.FromHexString(key), KeyBlobFormat.RawPublicKey);

      return ed25519.Verify(pub, data, sig);
    }

    // Decrypt decrypts the encrypted license file using the license key.
    public Dataset Decrypt(string key)
    {
      if (certificate.Alg != "aes-256-gcm+ed25519")
      {
        throw new Exception("license file must be encrypted");
      }

      var (encodedCiphertext, encodedIv, encodedTag) = certificate.Enc.Split(".", 3);
      var ciphertext = Convert.FromBase64String(encodedCiphertext);
      var iv = Convert.FromBase64String(encodedIv);
      var tag = Convert.FromBase64String(encodedTag);

      // Hash license key to get decryption secret
      var licenseKeyBytes = Encoding.UTF8.GetBytes(key);
      var sha256 = new Sha256();
      var secret = sha256.Hash(licenseKeyBytes);

      // Init AES-GCM
      var parameters = new AeadParameters(new KeyParameter(secret), 128, iv);
      var aes = new AesEngine();
      var cipher = new GcmBlockCipher(aes);

      cipher.Init(false, parameters);

      // Concat auth tag to ciphertext
      var input = ciphertext.Concat(tag).ToArray();
      var output = new byte[cipher.GetOutputSize(input.Length)];

      // Decrypt
      var len = cipher.ProcessBytes(input, 0, input.Length, output, 0);
      cipher.DoFinal(output, len);

      // Convert decrypted bytes to string
      var plaintext = Encoding.UTF8.GetString(output);

      return Deserialize(plaintext);
    }

    public Dataset Decode()
    {
      if (certificate.Alg != "base64+ed25519")
      {
        throw new Exception("license file must be encoded");
      }

      var dec = Convert.FromBase64String(certificate.Enc);
      var s = Encoding.UTF8.GetString(dec);

      return Deserialize(s);
    }

    private Dataset Deserialize(string json)
    {
      var document = JsonSerializer.Deserialize<Document>(json, new JsonSerializerOptions
      {
        PropertyNameCaseInsensitive = true
      });

      return new Dataset()
      {
        License = document.Data,
        Entitlements = document.Included,
      };
    }

    internal class Certificate
    {
      public string Alg { get; set; }
      public string Enc { get; set; }
      public string Sig { get; set; }
    }

    internal class Document
    {
      public License Data { get; set; }
      // TODO(ezekg) Included should really be polymorphic (i.e. it can have more than just entitlements)
      public List<Entitlement> Included { get; set; } = new();
    }

    public class Dataset
    {
      public License License { get; set; }
      public List<Entitlement> Entitlements { get; set; } = new();

      public bool Expired
      {
        get
        {
          return false;
        }
      }
    }
  }
}
