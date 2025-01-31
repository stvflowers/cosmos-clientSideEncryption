using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Encryption;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.Azure.Cosmos.Fluent;

/*
///////////////////////////////////////////////////////////////////////////////
/// Prerequisites
- Create Azure Key vault and new key
- Create a new user managed identity 
- Create a new Azure Cosmos DB account with Client-Side Encryption enabled using user managing identity
- Create client encryption key using PowerShell

/// PowerShell to create new key
$myKeyWrapMetadataObject = [Microsoft.Azure.Commands.CosmosDB.Models.PSSqlKeyWrapMetadata]::new([Microsoft.Azure.Management.CosmosDB.Models.KeyWrapMetadata]::new("my-key","AZURE_KEY_VAULT", "https://stflower-keyvault.vault.azure.net/keys/my-key/60b0ff442659463ead66434aabc40eb0", "RSA-OAEP"))
New-AzCosmosDbClientEncryptionKey -AccountName cosmos-nosql-cmk -DatabaseName myDatabase -ResourceGroupName rg-data -Name my-key -EncryptionAlgorithmName "AEAD_AES_256_CBC_HMAC_SHA256" -KeyWrapMetadata $myKeyWrapMetadataObject
Get-AzCosmosDbClientEncryptionKey -AccountName cosmos-nosql-cmk -DatabaseName myDatabase -ResourceGroupName rg-data
*/

////////////////////////////////////////////////////////////////////
//// Variables

string cosmosEndpoint = "https://cosmos-nosql-cmk.documents.azure.com:443/";
string cosmosDatabase = "myDatabase";
string cosmosContainer = "myContainer";
//string cosmosKeyName = "my-key";


////////////////////////////////////////////////////////////////////
//// Working with Cosmos DB Encryption

DefaultAzureCredentialOptions credOptions = new()
{
    ExcludeVisualStudioCodeCredential = true,
    ExcludeVisualStudioCredential = true,
    ExcludeEnvironmentCredential = true,
};

DefaultAzureCredential cred = new(credOptions);

var keyResolver = new KeyResolver(cred);

CosmosClient cosmosClient = CreateCosmosClient(cosmosEndpoint, cred);
var database = cosmosClient.GetDatabase(cosmosDatabase);
Container container = database.GetContainer(cosmosContainer);

#region Commented out code
///
///
// This won't work with Entra ID which blocks non-data operations
// Instead, I've deployed a new Cosmos DB with CMK
// Existing accounts can also be CMK enabled
//
// try {
//     await database.CreateClientEncryptionKeyAsync(
//         cosmosKeyName,
//         DataEncryptionAlgorithm.AeadAes256CbcHmacSha256,
//         new EncryptionKeyWrapMetadata(
//             KeyEncryptionKeyResolverName.AzureKeyVault,
//             akvKey,
//             akvUri,
//             EncryptionAlgorithm.RsaOaep.ToString()));
// } catch (CosmosException ex)
// {
//     Console.WriteLine(ex.Message);
//     Environment.Exit(1);
// }
#endregion

#region Create container
/////////////////////////////////////////////////////////////////////////
/// This only needs to be ran once, to create the container
/// 
// var path1 = new ClientEncryptionIncludedPath
// {
//     Path = "/property1",
//     ClientEncryptionKeyId = cosmosKeyName,
//     EncryptionType = EncryptionType.Deterministic.ToString(),
//     EncryptionAlgorithm = DataEncryptionAlgorithm.AeadAes256CbcHmacSha256
// };
// var path2 = new ClientEncryptionIncludedPath
// {
//     Path = "/property2",
//     ClientEncryptionKeyId = cosmosKeyName,
//     EncryptionType = EncryptionType.Randomized.ToString(),
//     EncryptionAlgorithm = DataEncryptionAlgorithm.AeadAes256CbcHmacSha256
// };
// await database.DefineContainer(cosmosContainer, "/partitionKey")
//     .WithClientEncryptionPolicy()
//     .WithIncludedPath(path1)
//     .WithIncludedPath(path2)
//     .Attach()
//     .CreateAsync();

#endregion


//// Write a new item with encrypted fields
MyDataClass myData = new()
{
    Property1 = "value1",
    Property2 = "value2",
    Property3 = "value3",
    PartitionKey = "pk1",
    Timestamp = DateTime.UtcNow
};
var writeResponse = container.CreateItemAsync(myData, new PartitionKey(myData.PartitionKey)).Result;
Console.WriteLine($"Write response: {writeResponse.StatusCode}");

//// Read an item with encrypted fields
var readResponse = container.ReadItemAsync<MyDataClass>(writeResponse.Resource.Id.ToString(), new PartitionKey(writeResponse.Resource.PartitionKey)).Result;
Console.WriteLine($"Read response: {readResponse.StatusCode}");



////////////////////////////////////////////////////////////////////
//// Methods

CosmosClient CreateCosmosClient(string endpoint, DefaultAzureCredential cred)
{
    try{

        CosmosSerializationOptions serializationOptions = new()
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        };

        CosmosClient cosmosClient = new CosmosClientBuilder(endpoint, cred)
            .WithSerializerOptions(serializationOptions)
            .Build()
            .WithEncryption(keyResolver, KeyEncryptionKeyResolverName.AzureKeyVault);

        return cosmosClient;

    } catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
        throw;
    }

}


////////////////////////////////////////////////////////////////////
//// Classes

class MyDataClass
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Property1 { get; set; }
    public string? Property2 { get; set; }
    public string? Property3 { get; set; }
    public string? PartitionKey { get; set; }
    public DateTime? Timestamp { get; set; }
}