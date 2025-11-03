using System.IO;
using System.Text;

internal static class NetworkSpawner
{
    public static void HandleSpawn(byte[] payload)
    {
        MemoryStream stream = new MemoryStream(payload);
        BinaryReader reader = new BinaryReader(stream);

        reader.ReadInt32();//ReadPackID

        int NetObjID = reader.ReadInt32();
        int spawnerID = reader.ReadInt32();

        int nameLen = reader.ReadInt32();
        byte[] namebytes = reader.ReadBytes(nameLen);

        string objectName = Encoding.UTF8.GetString(namebytes);
        Vector3 position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        Quaternion rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

        GigNet.Log?.Invoke($"Attempting to Spawn {objectName}");

        //var prefab = Resources.Load<GameObject>(objectName);
        var prefab = ObjectExist(objectName);

        // Object spawned = null;
        if (!prefab)
        {
            GigNet.LogWarning?.Invoke("Could not find the object to Spawn");
            return;
        }
        else
        {
            //spawned = UnityEngine.Object.Instantiate(prefab, position, rotation);
        }

        //if (!spawned)
        //{
        //    GigNet.LogWarning?.Invoke($"Object=={objectName.ToUpper()}==not found");
        //}
        //else
        //{
        //    NetworkObject networkObject = spawned.GetComponent<NetworkObject>();
        //    if (networkObject)
        //    {
        //        networkObject.SetID(NetObjID, spawnerID);
        //        networkObject.Register();
        //    }
        //}

        stream.Close();
    }

    static bool ObjectExist(string name)
    {
        return false;
    }
}