using keygen = Keygen;
using System;
using System.Linq;

const string KEYGEN_PUBLIC_KEY = "e8601e48b69383ba520245fd07971e983d06d22c4257cfd82304601479cee788";
const string KEYGEN_ACCOUNT_ID = "demo";
const string KEYGEN_LICENSE_KEY = "22DB40-963565-934484-12752B-8DA1C5-V3";

// Initialize Keygen API client
var client = new keygen.Client(account: KEYGEN_ACCOUNT_ID, key: KEYGEN_LICENSE_KEY);

// Retrieve the license object
var license = await client.Me();
Console.WriteLine($"license={license.ID}");

// Validate the license
Console.WriteLine("Validating license...");

var validation = await license.Validate();
Console.WriteLine($"validation={validation.Code}");

keygen.Machine machine = null;
if (validation.NotActivated)
{
  // Activate if not already activated
  Console.WriteLine("Activating license...");

  machine = await license.Activate();
  Console.WriteLine($"machine={machine.ID}");
}

// Retrieve the license's entitlements
var entitlements = await license.Entitlements();
Console.WriteLine(
  $@"entitlements={string.Join(",", entitlements.Select(e => e.Attributes.Code))}"
);

// Check if offline support is enabled
if (entitlements.Any(e => e.Attributes.Code == "OFFLINE_SUPPORT"))
{
  Console.WriteLine("Offline support is enabled!");
  Console.WriteLine("Checking out offline license file...");

  // Checkout a license file for offline use
  var lic = await license.Checkout();

  // Verify the license file
  if (lic.Verify(key: KEYGEN_PUBLIC_KEY))
  {
    Console.WriteLine("License file is valid!");

    // Decrypt the license file
    var dataset = lic.Decrypt(key: KEYGEN_LICENSE_KEY);

    Console.WriteLine($"license={dataset.License.ID}");
    Console.WriteLine(
      $@"entitlements={string.Join(",", dataset.Entitlements.Select(e => e.Attributes.Code))}"
    );
  }
  else
  {
    Console.WriteLine("License file is invalid!");
  }

  if (machine != null)
  {
    Console.WriteLine("Deactivating license...");

    // Deactivate
    await machine.Deactivate();
  }
}
else
{
  Console.WriteLine("Offline support is disabled!");
}
