using System.Linq;
using UnityEngine;

public class GpuTester : MonoBehaviour {
    public ComputeShader computeShader;
    public int[] keys; // Keys for dictionary, set in the inspector

    private ComputeBuffer resultBuffer;
    private GPUDictionary<TestStruct> dictionary;   //Gpu dictionary class
    
    private TestStruct[] resultData;
    
    void Start() {
        TestStruct[] testStructs = keys.Select(x =>  new TestStruct(x, (x+1)/100f)).ToArray(); 
        CreateDictionary(testStructs);
        TestOutput();
        dictionary.Release();
    }
    
    struct TestStruct      //mirror of GPU struct to set the data
    {
        public int a;
        public float b;
        public Vector2 mec;
        public TestStruct(int a, float b) {
            this.a = a;
            this.b = b;
            mec = Vector2.one;
        }
    };
    
    void CreateDictionary(TestStruct[] testStructs) {
        dictionary = new GPUDictionary<TestStruct>();
        dictionary.CreateDictionary(keys, testStructs, keys.Length, "test");   //pass parameters and same named as was used in hlsl
        dictionary.AddToShader(computeShader, computeShader.FindKernel("CSMain"));   // Call add to shader to bind buffers with specified Kernel 
    }
    
    //unsafe because of sizeof(TestStruct), can remove it if stride is calculated manually
    unsafe void TestOutput() {
        resultData = new TestStruct[keys.Length];
        resultBuffer = new ComputeBuffer(keys.Length, sizeof(TestStruct));
        
        computeShader.SetBuffer(0, "Result", resultBuffer);
        computeShader.SetInt("count", keys.Length);
        
        int threadGroupsX = Mathf.CeilToInt(keys.Length / 8.0f);
        computeShader.Dispatch(0, threadGroupsX, 1, 1);
        
        resultBuffer.GetData(resultData);
        
        for (int i = 0; i < keys.Length; i++) {
            Debug.Log($"Result[{i}] = {resultData[i].a} {resultData[i].b} {resultData[i].mec}");
        }

        resultBuffer.Release();
    }
}