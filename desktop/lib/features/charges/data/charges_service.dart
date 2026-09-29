import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/charges/models/charge_summary.dart';
import 'package:avelri_gestao/features/charges/models/payment_details.dart';

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
    required PaymentDetails payment,
  }) async {
    await _apiClient.patch(
      'api/charges/$chargeId/confirm-payment',
      body: payment.toJson(),
    );
  }
}
