using ContentBL.DTOs;
using ContentBL.Interfaces;
using ContentDA.Entities;
using ContentDA.Interfaces;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Results;

namespace ContentBL.Services;

public class LearningProgressService : ILearningProgressService
{
    private readonly ILearningProgressRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public LearningProgressService(ILearningProgressRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<List<int>>> GetCompletedLessonIdsAsync(Guid userId, CancellationToken ct = default)
    {
        var ids = await _repository.GetCompletedLessonIdsAsync(userId, ct);
        return Result<List<int>>.Success(ids);
    }

    public async Task<Result<LessonProgressStatusResponse>> GetLessonProgressAsync(Guid userId, int lessonId, CancellationToken ct = default)
    {
        var progress = await _repository.GetByUserAndLessonAsync(userId, lessonId, ct);

        // مفيش صف = لسه مخلّصش الدرس - ده رد صحيح ومتوقع، مش خطأ
        var response = new LessonProgressStatusResponse(lessonId, progress is not null, progress?.UpdatedAt);
        return Result<LessonProgressStatusResponse>.Success(response);
    }

    public async Task<Result<LessonProgressResponse>> MarkLessonCompleteAsync(Guid userId, int lessonId, CancellationToken ct = default)
    {
        var existing = await _repository.GetByUserAndLessonAsync(userId, lessonId, ct);
        if (existing is not null)
            return Result<LessonProgressResponse>.Success(new LessonProgressResponse(lessonId, existing.UpdatedAt));

        var progress = new LearningProgress
        {
            UserId = userId,
            LessonId = lessonId,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(progress, ct);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // إما الـ LessonId مش موجود (FK)، أو حصل Race Condition ونفس الصف اتضاف من طلب متزامن
            // (الـ Unique Constraint هي اللي بتمنع الـ Duplicate فعليًا وقت الحفظ)
            return Result<LessonProgressResponse>.Failure("الدرس غير موجود");
        }

        return Result<LessonProgressResponse>.Success(new LessonProgressResponse(lessonId, progress.UpdatedAt));
    }
}
