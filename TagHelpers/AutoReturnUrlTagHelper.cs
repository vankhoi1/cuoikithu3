using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace QuanLyThuVien.TagHelpers// Thay "QuanLyThuVien" bằng tên namespace dự án của bạn
{
    [HtmlTargetElement("form")]
    public class AutoReturnUrlTagHelper : TagHelper
    {
        [ViewContext]
        public ViewContext ViewContext { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            // Chỉ thêm vào các form dùng phương thức POST để tránh thừa
            var method = output.Attributes["method"]?.Value.ToString();
            if (string.Equals(method, "post", System.StringComparison.OrdinalIgnoreCase))
            {
                var returnUrl = ViewContext.HttpContext.Request.Path + ViewContext.HttpContext.Request.QueryString;

                var hiddenInput = new TagBuilder("input");
                hiddenInput.Attributes.Add("type", "hidden");
                hiddenInput.Attributes.Add("name", "returnUrl");
                hiddenInput.Attributes.Add("value", returnUrl);

                output.PostContent.AppendHtml(hiddenInput);
            }
        }
    }
}