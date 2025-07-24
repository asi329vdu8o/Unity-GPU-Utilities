using System;
using UnityEngine;

public class GPUDictionary<T> where T : unmanaged {
    
    #region Public Properties
    
    public int StoredElementCount { get; private set;}
    public int TableSize { get; private set;}
    public int TotalCollisions { get; private set;}
    public int MaxCollisionsInOneKey { get; private set;}
    public bool StoreCpuCopy{ get; private set;}
    
    #endregion
    
    #region Private Fields
    
    private ComputeBuffer indexMap;
    private ComputeBuffer storedValues;
    private int byteSizeOfElement;
    private uint[] hashedKeyToIndex;
    private GPUKeyValue<T>[] valueMap;
    private string dictionaryName;
    
    #endregion
    
    #region Constant Values
    
    private const string ValueMapName = "_valueMap";
    private const string IndexDataBufferName = "_indexDataBuffer";
    private const string TableSizeName = "_tableSize";
    private const string CustomElementByteSizeName = "_customElementByteSize";
    
    #endregion
    
    #region Public Methods
    
    public void Release() {
        indexMap?.Release();
        storedValues?.Release();

        MaxCollisionsInOneKey = 0;
        TotalCollisions = 0;
        TableSize = 0;
        StoredElementCount = 0;
        
        indexMap = null;
        storedValues = null;
    }
    
    public unsafe void CreateDictionary(int[] keys, T[] values, int tableSize, string dictionaryName, bool storeCPUside = false) {
        Release();

        if (keys.Length != values.Length) {
            throw (new Exception("keys.Length != values.Length"));
        }

        this.StoreCpuCopy = storeCPUside;
        this.dictionaryName = dictionaryName;
        this.TableSize = tableSize;
        this.StoredElementCount = keys.Length;
        this.byteSizeOfElement = sizeof(T);
        
        indexMap = new ComputeBuffer(tableSize, sizeof(uint));
        storedValues = new ComputeBuffer(keys.Length, sizeof(T) + sizeof(int));
        
        (uint hashedKey, int key, T value)[] orderedHashedKeys = new (uint hashedKey, int key, T value)[StoredElementCount];
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

        for (int i = 0; i < StoredElementCount; i++) {
            if (hashedKeyToIndex[orderedHashedKeys[i].hashedKey] == uint.MaxValue)
                hashedKeyToIndex[orderedHashedKeys[i].hashedKey] = BitPackLengthAndStartingIndex((uint)i, 1);
            else {
                TotalCollisions++;
                BitPackPlusOneLength(ref hashedKeyToIndex[orderedHashedKeys[i].hashedKey]);
                uint collisionsHere = (hashedKeyToIndex[orderedHashedKeys[i].hashedKey] >> 24) - 1;
                if (collisionsHere > MaxCollisionsInOneKey) MaxCollisionsInOneKey = (int)collisionsHere;
            }
        }
        indexMap.SetData(hashedKeyToIndex);
        
        valueMap = new GPUKeyValue<T>[orderedHashedKeys.Length];
        for (int i = 0; i < StoredElementCount; i++) {
            valueMap[i].Key = orderedHashedKeys[i].key;
            valueMap[i].Value = orderedHashedKeys[i].value;
        }
        storedValues.SetData(valueMap);

        if (!storeCPUside) {
            hashedKeyToIndex = null;
            storedValues = null;
        }
    }
    
    public void AddToMaterial(Material material) {
        if (material == null) throw new ArgumentNullException();
        
        material.SetBuffer(dictionaryName + ValueMapName, storedValues);
        material.SetBuffer(dictionaryName + IndexDataBufferName, indexMap);
        material.SetInt(dictionaryName + TableSizeName, TableSize);
        material.SetInt(dictionaryName + CustomElementByteSizeName, byteSizeOfElement);
    }

    public void AddToShader(ComputeShader shader, int kernelIndex) {
        if (shader != null) {
            shader.SetBuffer(kernelIndex, dictionaryName + ValueMapName, storedValues);
            shader.SetBuffer(kernelIndex, dictionaryName + IndexDataBufferName, indexMap);
            shader.SetInt(dictionaryName + TableSizeName, TableSize);
            shader.SetInt(dictionaryName + CustomElementByteSizeName, byteSizeOfElement);
        }
        else {
            throw new NullReferenceException();
        }
    }
    
    //NOT UPDATED
    public bool TryGetValue(int key, out T value) {
        value = default(T);
        if (!StoreCpuCopy) {
            Debug.LogError("CPU copy is not active.");
            return false;
        }

        uint hash = IndexHash(key);
        
        if (hashedKeyToIndex[hash] == uint.MaxValue) 
            return false;
        
        uint length = hashedKeyToIndex[hash] >> 24;
        uint startingIndex = hashedKeyToIndex[hash] - (length << 24);

        if (length == 1) {
            if (valueMap[startingIndex].Key == key)
                return true;
        }
        else {
            return BinarySearch(key, startingIndex, length, ref value);
        }

        return false;
    }
    
    #endregion
    
    #region Private Methods
    
    private bool BinarySearch(int key, uint starting, uint length, ref T value) {
        uint min = starting;
        uint max = starting + length - 1;
        
        while (min != max) {
            uint index = min + (max - min) / 2;
            
            if (valueMap[index].Key < key) {
                min = index + 1;
            }else if (valueMap[index].Key > key) {
                max = index;
            }
            else {
                value = valueMap[index].Value;
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
    
    private uint IndexHash(int key) {
        return (uint)key  * 10 % (uint)TableSize;
    }
    
    #endregion
    
    ~GPUDictionary() {
        Release();
    }
}

internal struct GPUKeyValue<TValue> where TValue : unmanaged {
    internal int Key;
    internal TValue Value;
}
  