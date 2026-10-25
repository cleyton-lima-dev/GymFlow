import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/charges/models/charge_summary.dart';
import 'package:avelri_gestao/features/charges/models/payment_details.dart';

class ChargesService {
  const ChargesService(this._apiClient);

  final ApiClient _apiClient;

  Future<List<ChargeSummary>> getCharges({DateTime? paidMonth}) async {
    final query = paidMonth == null
        ? ''
        : '?paidMonth=${_formatDateOnly(paidMonth)}';
    final response = await _apiClient.get('api/charges$query');

    if (response is! List) {
      throw const FormatException('Invalid charges response.');
    }

    return response
        .map(
          (item) =>
              ChargeSummary.fromJson(Map<String, dynamic>.from(item as Map)),
        )
        .toList();
  }

  String _formatDateOnly(DateTime date) =>
      '${date.year.toString().padLeft(4, '0')}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}';

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
