using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

public class OnnxImageService
{
    private readonly InferenceSession _session;

    // Constructor: Tải mô hình ONNX vào bộ nhớ khi service được khởi tạo
    public OnnxImageService()
    {
        var modelPath = Path.Combine(Directory.GetCurrentDirectory(), "MLModels", "mobilenetv2-7.onnx");

        // Sửa lỗi 1: Ghi rõ tên đầy đủ của SessionOptions từ thư viện ONNX
        var options = new Microsoft.ML.OnnxRuntime.SessionOptions();
        options.AppendExecutionProvider_CPU(0);

        // Sửa lỗi 2: Dòng RegisterCustomOpsLibrary không cần thiết cho model này nên đã bị xóa.

        _session = new InferenceSession(modelPath, options);
    }

    public async Task<float[]> GetImageVectorAsync(Stream imageStream)
    {
        // 1. Đọc và tiền xử lý ảnh
        // Các mô hình AI yêu cầu ảnh đầu vào phải có kích thước và định dạng nhất định
        using var image = await Image.LoadAsync<Rgb24>(imageStream);

        // MobileNetv2 yêu cầu ảnh 224x224
        image.Mutate(x =>
        {
            x.Resize(new ResizeOptions
            {
                Size = new Size(224, 224),
                Mode = ResizeMode.Crop
            });
        });

        // 2. Chuyển ảnh thành Tensor (định dạng mà model hiểu được)
        // Kích thước tensor: [batch_size, channels, height, width]
        var tensor = new DenseTensor<float>(new[] { 1, 3, 224, 224 });
        var mean = new[] { 0.485f, 0.456f, 0.406f };
        var stdDev = new[] { 0.229f, 0.224f, 0.225f };

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var pixelRow = accessor.GetRowSpan(y);
                for (int x = 0; x < accessor.Width; x++)
                {
                    var pixel = pixelRow[x];
                    tensor[0, 0, y, x] = ((pixel.R / 255f) - mean[0]) / stdDev[0];
                    tensor[0, 1, y, x] = ((pixel.G / 255f) - mean[1]) / stdDev[1];
                    tensor[0, 2, y, x] = ((pixel.B / 255f) - mean[2]) / stdDev[2];
                }
            }
        });

        // 3. Chuẩn bị đầu vào và chạy model
        // Tên "input" phải khớp với tên đầu vào của mô hình ONNX
        // ...
        // Tên đầu vào của model MobileNetv2 là "data"
        var inputs = new List<NamedOnnxValue>
{
    NamedOnnxValue.CreateFromTensor("data", tensor) // <-- SỬA "input" THÀNH "data"
};

        using var results = _session.Run(inputs);

        // Lấy kết quả đầu tiên là cách an toàn và đơn giản nhất
        var output = results.FirstOrDefault(); // <-- SỬA LẠI THÀNH DÒNG NÀY
                                               //...
        if (output != null)
        {
            return output.AsTensor<float>().ToArray();
        }

        return null;
    }
}