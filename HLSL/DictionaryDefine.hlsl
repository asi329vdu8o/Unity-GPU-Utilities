
#define DICTIONARY(DICTIONARYNAME, TYPE_STRUCT_PH)                              \
                                                                                    \
struct DICTIONARYNAME##KeyValuePair{                                                   \
int key;                                                                          \
TYPE_STRUCT_PH structValue;                                                              \
};                                                                                       \
StructuredBuffer<DICTIONARYNAME##KeyValuePair> DICTIONARYNAME##_valueMap : register(t0); \
StructuredBuffer<uint> DICTIONARYNAME##_indexDataBuffer : register(t1);                \
                                                                                      \
uint DICTIONARYNAME##_tableSize;                                                      \
                                                                                      \
                                                                                        \
uint DICTIONARYNAME##_hash(int key)                                                   \
{                                                                                     \
    return (uint)(key * 10) % DICTIONARYNAME##_tableSize;                             \
}                                                                                     \
                                                                                      \
bool DICTIONARYNAME##_BinarySearch(int key, int starting, int length, out TYPE_STRUCT_PH readStruct)        \
{                                                                                     \
    uint min = starting;                                                               \
    uint max = starting + length;                                                      \
                                                                               \
    while (min < max) {                                                                \
        uint index = min + (max - min) / 2;                                           \
                                                                                      \
        DICTIONARYNAME##KeyValuePair keyValuePair = DICTIONARYNAME##_valueMap[index];  \
                                                                                      \
        if (keyValuePair.key < key) {                                                         \
            min = index + 1;                                                           \
        } else if (keyValuePair.key > key) {                                                  \
            max = index;                                                               \
        } else {                                                                       \
            readStruct = keyValuePair.structValue;\
            return true;                                        \
        }                                                                             \
    }                                                                                 \
    return false;                                                                \
}                                                                                     \
                                                                                      \
bool DICTIONARYNAME##_GetValue(int key, out TYPE_STRUCT_PH readStruct)                                     \
{                                                                                     \
    uint index = DICTIONARYNAME##_hash(key);                                          \
    uint indexData = DICTIONARYNAME##_indexDataBuffer[index];                         \
                                                        \
    if (indexData == 4294967295U) return false;                            \
                                                                                      \
    uint length = indexData >> 24;                                                     \
    uint startingIndex = indexData & 0xFFFFFFu;                                       \
                                                                                  \
    return DICTIONARYNAME##_BinarySearch(key, startingIndex, length, readStruct);                 \
}