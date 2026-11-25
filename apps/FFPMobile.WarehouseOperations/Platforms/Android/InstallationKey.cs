using Android.Runtime;
using Android.Security.Keystore;
using FFPMobile.Shared.WarehouseProtocol;
using Java.Security;
using Java.Security.Spec;

namespace FFPMobile.App;

internal sealed class InstallationKey : IInstallationKey
{
    private const string Alias = "org.ffp.mobile.warehouse.installation.v1";
    private readonly object gate = new();

    public byte[] PublicKey
    {
        get
        {
            lock (gate)
            {
                using var store = Open();
                using var certificate = store.GetCertificate(Alias);
                using var key = certificate!.PublicKey;
                return key!.GetEncoded()!;
            }
        }
    }

    public byte[] Sign(byte[] data)
    {
        lock (gate)
        {
            using var store = Open();
            using var key = store.GetKey(Alias, null)?.JavaCast<IPrivateKey>()
                ?? throw new KeyStoreException("The installation signing key is unavailable.");
            using var signature = Signature.GetInstance("SHA256withECDSA")!;
            signature.InitSign(key);
            signature.Update(data);
            return signature.Sign()!;
        }
    }

    private static KeyStore Open()
    {
        var store = KeyStore.GetInstance("AndroidKeyStore")!;
        store.Load(null);
        if (store.ContainsAlias(Alias)) return store;
        using var generator = KeyPairGenerator.GetInstance(KeyProperties.KeyAlgorithmEc, "AndroidKeyStore")!;
        using var curve = new ECGenParameterSpec("secp256r1");
        using var parameters = new KeyGenParameterSpec.Builder(Alias, KeyStorePurpose.Sign)
            .SetAlgorithmParameterSpec(curve)!
            .SetDigests(KeyProperties.DigestSha256)!
            .Build();
        generator.Initialize(parameters);
        using var pair = generator.GenerateKeyPair();
        return store;
    }
}
