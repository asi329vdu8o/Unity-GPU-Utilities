using System;
using System.Linq;
using UnityEngine;

public struct GPUKeyValue<TValue> where TValue : unmanaged {
    public int key;
    public TValue value;
}

public class GPUDictionary<T> where T : unmanaged {
    
    private ComputeBuffer indexMap;
    public ComputeBuffer storedValues;

    private int storedElementCount;
    private int tableSize;

    public int totalCollisions;
    public int maxCollisionsInOneKey;

    public bool storeCPUside = true;

    private int byteSizeOfElement;
    
    private const string valueMapName = "_valueMap";
    private const string indexDataBufferName = "_indexDataBuffer";
    private const string tableSizeName = "_tableSize";
    private const string customElementByteSizeName = "_customElementByteSize";
    
    private uint[] hashedKeyToIndex;
    private GPUKeyValue<T>[] valueMap;
    private string dictionaryName;

    
    ~GPUDictionary() {
        Debug.Log("Releasing Dictionary");
        Release();
    }
    
    public void Release() {
        indexMap?.Release();
        storedValues?.Release();

        maxCollisionsInOneKey = 0;
        totalCollisions = 0;
        tableSize = 0;
        storedElementCount = 0;
        
        indexMap = null;
        storedValues = null;
    }
    
    public unsafe void CreateDictionary(int[] keys, T[] values, int tableSize, string dictionaryName) {
        Release();

        if (keys.Length != values.Length) {
            throw (new System.Exception("keys.Length != values.Length"));
        }
        
        this.dictionaryName = dictionaryName;
        this.tableSize = tableSize;
        this.storedElementCount = keys.Length;
        this.byteSizeOfElement = sizeof(T);
        
        indexMap = new ComputeBuffer(tableSize, sizeof(uint));
        storedValues = new ComputeBuffer(keys.Length, sizeof(T) + sizeof(int));
        
        (uint hashedKey, int key, T value)[] orderedHashedKeys = new (uint hashedKey, int key, T value)[storedElementCount];
        for (int i = 0; i < keys.Length; i++) {
            orderedHashedKeys[i].hashedKey = IndexHash(keys[i]);
            orderedHashedKeys[i].key = keys[i];
            orderedHashedKeys[i].value = values[i];
        }
        Array.Sort(orderedHashedKeys);
        
        hashedKeyToIndex = new uint[tableSize];
        for (int i = 0; i < tableSize; i++) {
            hashedKeyToIndex[i] = uint.MaxValue;
        }

        for (int i = 0; i < storedElementCount; i++) {
            if (hashedKeyToIndex[orderedHashedKeys[i].hashedKey] == uint.MaxValue)
                hashedKeyToIndex[orderedHashedKeys[i].hashedKey] = BitPackLengthAndStartingIndex((uint)i, 1);
            else {
                totalCollisions++;
                BitPackPlusOneLength(ref hashedKeyToIndex[orderedHashedKeys[i].hashedKey]);
                uint collisionsHere = (hashedKeyToIndex[orderedHashedKeys[i].hashedKey] >> 24) - 1;
                if (collisionsHere > maxCollisionsInOneKey) maxCollisionsInOneKey = (int)collisionsHere;
            }
        }
        indexMap.SetData(hashedKeyToIndex);
        
        valueMap = new GPUKeyValue<T>[orderedHashedKeys.Length];
        for (int i = 0; i < storedElementCount; i++) {
            valueMap[i].key = orderedHashedKeys[i].key;
            valueMap[i].value = orderedHashedKeys[i].value;
        }
        storedValues.SetData(valueMap);

        if (!storeCPUside) {
            hashedKeyToIndex = null;
            storedValues = null;
        }
    }

    //NOT UPDATED
    public bool TryGetValue(int key, out T value) {
        value = default(T);
        if (!storeCPUside) {
            Debug.LogError("CPU copy is not active.");
            return false;
        }

        uint hash = IndexHash(key);
        
        if (hashedKeyToIndex[hash] == uint.MaxValue) 
            return false;
        
        uint length = hashedKeyToIndex[hash] >> 24;
        uint startingIndex = hashedKeyToIndex[hash] - (length << 24);

        if (length == 1) {
            if (valueMap[startingIndex].key == key)
                return true;
        }
        else {
            return BinarySearch(key, startingIndex, length, ref value);
        }

        return false;
    }

    private bool BinarySearch(int key, uint starting, uint length, ref T value) {
        uint min = starting;
        uint max = starting + length - 1;
        
        while (min != max) {
            uint index = min + (max - min) / 2;
            
            if (valueMap[index].key < key) {
                min = index + 1;
            }else if (valueMap[index].key > key) {
                max = index;
            }
            else {
                value = valueMap[index].value;
                return true;
            }
        }
        return false;
    }
    private uint BitPackLengthAndStartingIndex(uint index, uint length) {
        uint output = 0;
        output += length << 24;
        output += index;
        return output;
    }

    private void BitPackPlusOneLength(ref uint currentValue) {
        currentValue += 1 << 24;
    }
   
    public void AddToMaterial(Material material) {
        if (material == null) throw new System.ArgumentNullException();
        
        material.SetBuffer(dictionaryName + valueMapName, storedValues);
        material.SetBuffer(dictionaryName + indexDataBufferName, indexMap);
        material.SetInt(dictionaryName + tableSizeName, tableSize);
        material.SetInt(dictionaryName + customElementByteSizeName, byteSizeOfElement);
    }

    public void AddToShader(ComputeShader shader, int kernelIndex) {
        if (shader != null) {
            shader.SetBuffer(kernelIndex, dictionaryName + valueMapName, storedValues);
            shader.SetBuffer(kernelIndex, dictionaryName + indexDataBufferName, indexMap);
            shader.SetInt(dictionaryName + tableSizeName, tableSize);
            shader.SetInt(dictionaryName + customElementByteSizeName, byteSizeOfElement);
        }
        else {
            throw new NullReferenceException();
        }
    }
    private uint IndexHash(int key) {
        return (uint)key  * 10 % (uint)tableSize;
    }
}
  