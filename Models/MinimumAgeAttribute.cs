using System;
using System.ComponentModel.DataAnnotations;

namespace QuanLyThuVien.Validation
{
    public class MinimumAgeAttribute : ValidationAttribute
    {
        private readonly int _minimumAge;

        public MinimumAgeAttribute(int minimumAge)
        {
            _minimumAge = minimumAge;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is DateTime dateOfBirth)
            {
                // Tính tuổi
                var today = DateTime.Today;
                var age = today.Year - dateOfBirth.Year;

                // Nếu chưa đến ngày sinh nhật trong năm nay thì trừ đi 1 tuổi
                if (dateOfBirth.Date > today.AddYears(-age))
                {
                    age--;
                }

                // So sánh với tuổi tối thiểu
                if (age < _minimumAge)
                {
                    return new ValidationResult(ErrorMessage ?? $"Bạn phải đủ {_minimumAge} tuổi.");
                }
            }

            return ValidationResult.Success;
        }
    }
}