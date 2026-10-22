import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/dashboard/models/dashboard_summary.dart';

class DashboardService {
  DashboardService(this._api);
  final ApiClient _api;

  Future<DashboardSummary> load({bool includeFinancial = true}) async =>
      DashboardSummary.fromJson(
        await _api.get(
          includeFinancial ? '/api/dashboard' : '/api/dashboard/operational',
        ) as Map<String, dynamic>,
      );
}
