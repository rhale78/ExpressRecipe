using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.MealPlanningService.Data;

namespace ExpressRecipe.MealPlanningService.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class MealPlanningController : ControllerBase
    {
        private readonly ILogger<MealPlanningController> _logger;
        private readonly IMealPlanningRepository _repository;

        public MealPlanningController(ILogger<MealPlanningController> logger, IMealPlanningRepository repository)
        {
            _logger = logger;
            _repository = repository;
        }

        private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpPost("plans")]
        public async Task<IActionResult> CreateMealPlan([FromBody] CreatePlanRequest request)
        {
            Guid userId = GetUserId();
            Guid planId = await _repository.CreateMealPlanAsync(userId, request.StartDate, request.EndDate, request.Name);
            MealPlanDto? plan = await _repository.GetMealPlanAsync(planId, userId);
            return CreatedAtAction(nameof(GetMealPlan), new { id = planId }, plan);
        }

        [HttpGet("plans")]
        public async Task<IActionResult> GetMealPlans()
        {
            Guid userId = GetUserId();
            List<MealPlanDto> plans = await _repository.GetUserMealPlansAsync(userId);
            return Ok(plans);
        }

        [HttpGet("plans/{id}")]
        public async Task<IActionResult> GetMealPlan(Guid id)
        {
            Guid userId = GetUserId();
            MealPlanDto? plan = await _repository.GetMealPlanAsync(id, userId);
            return plan == null ? NotFound() : Ok(plan);
        }

        [HttpDelete("plans/{id}")]
        public async Task<IActionResult> DeletePlan(Guid id)
        {
            Guid userId = GetUserId();
            await _repository.DeleteMealPlanAsync(id, userId);
            return NoContent();
        }

        [HttpPost("plans/{id}/meals")]
        public async Task<IActionResult> AddPlannedMeal(Guid id, [FromBody] AddMealRequest request)
        {
            Guid userId = GetUserId();
            Guid mealId = await _repository.AddPlannedMealAsync(
                id, userId, request.RecipeId, request.PlannedFor, request.MealType, request.Servings);
            return Ok(new { id = mealId });
        }

        [HttpGet("plans/{id}/meals")]
        public async Task<IActionResult> GetPlannedMeals(Guid id, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            List<PlannedMealDto> meals = await _repository.GetPlannedMealsAsync(id, startDate, endDate);
            return Ok(meals);
        }

        [HttpPut("meals/{id}/complete")]
        public async Task<IActionResult> CompleteMeal(Guid id)
        {
            await _repository.MarkMealAsCompletedAsync(id);
            return NoContent();
        }

        [HttpPost("goals")]
        public async Task<IActionResult> SetGoal([FromBody] SetGoalRequest request)
        {
            Guid userId = GetUserId();
            Guid goalId = await _repository.SetNutritionalGoalAsync(
                userId, request.GoalType, request.TargetValue, request.Unit, request.StartDate, request.EndDate);
            return Ok(new { id = goalId });
        }

        [HttpGet("goals")]
        public async Task<IActionResult> GetGoals()
        {
            Guid userId = GetUserId();
            List<NutritionalGoalDto> goals = await _repository.GetUserGoalsAsync(userId);
            return Ok(goals);
        }

        [HttpGet("nutrition/summary")]
        public async Task<IActionResult> GetNutritionSummary([FromQuery] DateTime date)
        {
            Guid userId = GetUserId();
            NutritionSummaryDto summary = await _repository.GetNutritionSummaryAsync(userId, date);
            return Ok(summary);
        }
    }

    public class CreatePlanRequest
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? Name { get; set; }
    }

    public class AddMealRequest
    {
        public Guid RecipeId { get; set; }
        public DateTime PlannedFor { get; set; }
        public string MealType { get; set; } = "Dinner";
        public int Servings { get; set; } = 1;
    }

    public class SetGoalRequest
    {
        public string GoalType { get; set; } = string.Empty;
        public decimal TargetValue { get; set; }
        public string? Unit { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
