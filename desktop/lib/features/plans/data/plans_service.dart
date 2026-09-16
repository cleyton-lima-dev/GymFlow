import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/plans/models/plan_summary.dart';

class PlansService {
  const PlansService(this._apiClient);

  final ApiClient _apiClient;

  Future<List<PlanSummary>> getPlans({
    bool? isActive,
  }) async {
    final queryParameters = <String, String>{
      if (isActive != null) 'isActive': isActive.toString(),
    };

    final query = Uri(queryParameters: queryParameters).query;

    final path = query.isEmpty
        ? 'api/plans'
        : 'api/plans?$query';

    final response = await _apiClient.get(path);

    if (response is! List) {
      throw const FormatException('Invalid plans response.');
    }

    return response
        .map(
          (item) => PlanSummary.fromJson(
        Map<String, dynamic>.from(item as Map),
      ),
    )
        .toList();
  }

  Future<void> createPlan({
    required String name,
    required double price,
    required int durationMonths,
    required PlanBillingCycle billingCycle,
  }) async {
    await _apiClient.post(
      'api/plans',
      body: {
        'name': name.trim(),
        'price': price,
        'durationMonths': durationMonths,
        'billingCycle': billingCycle.value,
      },
    );
  }

  Future<void> updatePlan({
    required String planId,
    required String name,
    required double price,
    required int durationMonths,
    required PlanBillingCycle billingCycle,
  }) async {
    await _apiClient.put(
      'api/plans/$planId',
      body: {
        'name': name.trim(),
        'price': price,
        'durationMonths': durationMonths,
        'billingCycle': billingCycle.value,
      },
    );
  }

  Future<void> updatePlanStatus({
    required String planId,
    required bool isActive,
  }) async {
    await _apiClient.patch(
      'api/plans/$planId/status',
      body: {
        'isActive': isActive,
      },
    );
  }
}
