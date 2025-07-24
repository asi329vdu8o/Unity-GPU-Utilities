# Unity-GPU-Utilities

# Current Features:

-Generic value GPU Dictionary for hlsl:
Takes an int key and a generic value and allows you to retrieve the value with its associated key.

How to use:
1. Include Dictionary #define file
2. Declare "Dictionary(DictionaryName, structName) where => DictionaryName is the name that you want to assign it and structName must match the name of the type that you use as value
3. (In C#) Create a GPUDictionary<Struct> class
4. (In C#) Call GPUDictionary.CreateDictionary(keys, values, tablesize, name) where => keys and values must be the same length and ordered (so key[0] will be assigned to value[0] and name must match the hlsl side name you used. Tablesize allows you to adjust speed vs memory use, higher tablesize will use more memory but have less collisions. Recommended number is keys.Length for a compromise between the two.

(See examples folder for example implementation)



