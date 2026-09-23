import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/charges/models/charge_summary.dart';

class ChargesService {
  const ChargesService(this._apiClient);

  final ApiClient _apiClient;

  Future<List<ChargeSummary>> getCharges() async {
    final response = await _apiClient.get(
      'api/charges',
    );

    if (response is! List) {
      throw const FormatException(
        'Invalid charges response.',
      );
    }

    return response
        .map(
          (item) => ChargeSummary.fromJson(
        Map<String, dynamic>.from(
          item as Map,
        ),
      ),
    )
        .toList();
  }

  Future<void> confirmPayment({
    required String chargeId,
  }) async {
    await _apiClient.patch(
      'api/charges/$chargeId/confirm-payment',
      body: {},
    );
  }
}
