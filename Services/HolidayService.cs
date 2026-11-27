using LeaveManage.DTOs.Requests;
using LeaveManage.Exceptions;
using LeaveManage.Models;
using LeaveManage.Repositories;

namespace LeaveManage.Services;

/// <summary>Quản lý danh sách ngày lễ (Admin). Ngày lễ rơi vào T7/CN không cần lưu vì vốn đã không bị trừ phép.</summary>
public interface IHolidayService
{
    Task<List<Holiday>> GetAllAsync(int? year);
    Task<Holiday> CreateAsync(HolidayUpsertDto dto);
    Task DeleteAsync(int id);
}

public class HolidayService : IHolidayService
{
    private readonly IHolidayRepository _repository;

    public HolidayService(IHolidayRepository repository) => _repository = repository;

    public Task<List<Holiday>> GetAllAsync(int? year) => _repository.ListAsync(year);

    public async Task<Holiday> CreateAsync(HolidayUpsertDto dto)
    {
        var date = dto.Date!.Value.Date;
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            throw new BadRequestException("Ngày lễ rơi vào Thứ 7/Chủ nhật không cần khai báo.", "WEEKEND_HOLIDAY");
        if (await _repository.ExistsAsync(date))
            throw new ConflictException("Ngày này đã có trong lịch nghỉ lễ.", "HOLIDAY_EXISTS");

        var holiday = new Holiday { Date = date, Name = dto.Name.Trim() };
        await _repository.AddAsync(holiday);
        await _repository.SaveChangesAsync();
        return holiday;
    }

    public async Task DeleteAsync(int id)
    {
        var holiday = await _repository.GetByIdAsync(id)
            ?? throw new NotFoundException("Không tìm thấy ngày lễ.", "HOLIDAY_NOT_FOUND");
        _repository.Remove(holiday);
        await _repository.SaveChangesAsync();
    }
}
