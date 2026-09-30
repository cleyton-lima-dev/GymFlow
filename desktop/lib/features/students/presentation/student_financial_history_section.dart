import 'package:avelri_gestao/features/charges/models/charge_summary.dart';
import 'package:avelri_gestao/features/charges/models/payment_details.dart';
import 'package:avelri_gestao/features/enrollments/models/enrollment_summary.dart';
import 'package:avelri_gestao/features/students/models/student_financial_history_item.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

class StudentFinancialHistorySection extends StatelessWidget {
  const StudentFinancialHistorySection({
    required this.history,
    required this.isLoading,
    required this.errorMessage,
    required this.onRetry,
    required this.primaryColor,
    super.key,
  });

  final List<StudentFinancialHistoryItem> history;
  final bool isLoading;
  final String? errorMessage;
  final Future<void> Function() onRetry;
  final Color primaryColor;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(26),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(
          color: theme.dividerColor,
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 38,
                height: 38,
                decoration: BoxDecoration(
                  color: primaryColor.withValues(
                    alpha: 0.10,
                  ),
                  borderRadius:
                  BorderRadius.circular(10),
                ),
                child: Icon(
                  Icons.account_balance_wallet_outlined,
                  color: primaryColor,
                  size: 20,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Text(
                  'Histórico financeiro',
                  style: TextStyle(
                    color: colorScheme.onSurface,
                    fontSize: 16,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
              IconButton(
                tooltip: 'Atualizar histórico',
                onPressed:
                isLoading ? null : onRetry,
                icon: Icon(
                  Icons.refresh_rounded,
                  color: primaryColor,
                ),
              ),
            ],
          ),
          const SizedBox(height: 8),
          Text(
            'Matrículas, cobranças e pagamentos registrados para este aluno.',
            style: TextStyle(
              color: colorScheme.onSurfaceVariant,
              fontSize: 13,
            ),
          ),
          const SizedBox(height: 24),
          if (isLoading && history.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(
                vertical: 30,
              ),
              child: Center(
                child: CircularProgressIndicator(),
              ),
            )
          else if (errorMessage != null &&
              history.isEmpty)
            _FinancialHistoryError(
              message: errorMessage!,
              onRetry: onRetry,
            )
          else if (history.isEmpty)
              Padding(
                padding: const EdgeInsets.symmetric(
                  vertical: 30,
                ),
                child: Center(
                  child: Text(
                    'Nenhum histórico financeiro encontrado.',
                    style: TextStyle(
                      color:
                      colorScheme.onSurfaceVariant,
                    ),
                  ),
                ),
              )
            else ...[
                if (errorMessage != null) ...[
                  Text(
                    errorMessage!,
                    style: const TextStyle(
                      color: Color(0xFFB54752),
                      fontSize: 12,
                    ),
                  ),
                  const SizedBox(height: 16),
                ],
                ...history.map(
                      (item) => Padding(
                    padding:
                    const EdgeInsets.only(bottom: 14),
                    child: _FinancialHistoryCard(
                      item: item,
                      primaryColor: primaryColor,
                    ),
                  ),
                ),
              ],
        ],
      ),
    );
  }
}

class _FinancialHistoryCard extends StatelessWidget {
  const _FinancialHistoryCard({
    required this.item,
    required this.primaryColor,
  });

  final StudentFinancialHistoryItem item;
  final Color primaryColor;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    final currency = NumberFormat.currency(
      locale: 'pt_BR',
      symbol: 'R\$',
    );

    final dateFormat = DateFormat('dd/MM/yyyy');
    final dateTimeFormat =
    DateFormat('dd/MM/yyyy HH:mm');

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: colorScheme.surfaceContainerHighest,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(
          color: theme.dividerColor,
        ),
      ),
      child: Column(
        crossAxisAlignment:
        CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  item.planName,
                  style: TextStyle(
                    color: colorScheme.onSurface,
                    fontSize: 15,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
              _StatusBadge(
                label: _enrollmentStatusLabel(
                  item.enrollmentStatus,
                ),
                color: _enrollmentStatusColor(
                  item.enrollmentStatus,
                  primaryColor,
                ),
              ),
            ],
          ),
          const SizedBox(height: 14),
          Wrap(
            spacing: 28,
            runSpacing: 12,
            children: [
              _HistoryInfo(
                label: 'Período',
                value:
                '${dateFormat.format(item.startDate)} até ${dateFormat.format(item.endDate)}',
              ),
              _HistoryInfo(
                label: 'Valor do plano',
                value:
                currency.format(item.planPrice),
              ),
              if (item.cancellationDate != null)
                _HistoryInfo(
                  label: 'Cancelada em',
                  value: dateFormat.format(
                    item.cancellationDate!,
                  ),
                ),
            ],
          ),
          if (item.chargeId != null) ...[
            const SizedBox(height: 16),
            Divider(
              color: theme.dividerColor,
              height: 1,
            ),
            const SizedBox(height: 16),
            Row(
              children: [
                Expanded(
                  child: Text(
                    'Cobrança',
                    style: TextStyle(
                      color: colorScheme.onSurface,
                      fontSize: 13,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
                if (item.chargeStatus != null)
                  _StatusBadge(
                    label: _chargeStatusLabel(
                      item.chargeStatus!,
                    ),
                    color: _chargeStatusColor(
                      item.chargeStatus!,
                      primaryColor,
                    ),
                  ),
              ],
            ),
            const SizedBox(height: 14),
            Wrap(
              spacing: 28,
              runSpacing: 12,
              children: [
                if (item.chargeAmount != null)
                  _HistoryInfo(
                    label: 'Cobrança',
                    value: currency.format(
                      item.chargeAmount,
                    ),
                  ),
                if (item.dueDate != null)
                  _HistoryInfo(
                    label: 'Vencimento',
                    value: dateFormat.format(
                      item.dueDate!,
                    ),
                  ),
                if (item.paidAmount != null)
                  _HistoryInfo(
                    label: 'Valor pago',
                    value: currency.format(
                      item.paidAmount,
                    ),
                  ),
                if (item.discountAmount != null &&
                    item.discountAmount! > 0)
                  _HistoryInfo(
                    label: 'Desconto',
                    value: currency.format(
                      item.discountAmount,
                    ),
                  ),
                if (item.paymentMethod != null)
                  _HistoryInfo(
                    label: 'Forma de pagamento',
                    value: _paymentMethodLabel(
                      item.paymentMethod!,
                    ),
                  ),
                if (item.paidAt != null)
                  _HistoryInfo(
                    label: 'Pago em',
                    value: dateTimeFormat.format(
                      item.paidAt!.toLocal(),
                    ),
                  ),
              ],
            ),
          ],
        ],
      ),
    );
  }

  static String _enrollmentStatusLabel(
      EnrollmentStatus status,
      ) {
    return switch (status) {
      EnrollmentStatus.active => 'Ativa',
      EnrollmentStatus.cancelled => 'Cancelada',
      EnrollmentStatus.expired => 'Expirada',
      EnrollmentStatus.pendingPayment =>
      'Pagamento pendente',
    };
  }

  static Color _enrollmentStatusColor(
      EnrollmentStatus status,
      Color primaryColor,
      ) {
    return switch (status) {
      EnrollmentStatus.active => const Color(0xFF2CB67D),
      EnrollmentStatus.cancelled =>
      const Color(0xFFE35D6A),
      EnrollmentStatus.expired =>
      const Color(0xFF8A8F98),
      EnrollmentStatus.pendingPayment =>
      primaryColor,
    };
  }

  static String _chargeStatusLabel(
      ChargeStatus status,
      ) {
    return switch (status) {
      ChargeStatus.pending => 'Pendente',
      ChargeStatus.paid => 'Pago',
      ChargeStatus.overdue => 'Em atraso',
      ChargeStatus.cancelled => 'Cancelada',
    };
  }

  static Color _chargeStatusColor(
      ChargeStatus status,
      Color primaryColor,
      ) {
    return switch (status) {
      ChargeStatus.pending => primaryColor,
      ChargeStatus.paid => const Color(0xFF2CB67D),
      ChargeStatus.overdue =>
      const Color(0xFFE35D6A),
      ChargeStatus.cancelled =>
      const Color(0xFF8A8F98),
    };
  }

  static String _paymentMethodLabel(
      PaymentMethod method,
      ) {
    return method.label;
  }
}

class _HistoryInfo extends StatelessWidget {
  const _HistoryInfo({
    required this.label,
    required this.value,
  });

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final colorScheme =
        Theme.of(context).colorScheme;

    return SizedBox(
      width: 180,
      child: Column(
        crossAxisAlignment:
        CrossAxisAlignment.start,
        children: [
          Text(
            label,
            style: TextStyle(
              color: colorScheme.onSurfaceVariant,
              fontSize: 11,
              fontWeight: FontWeight.w600,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            value,
            style: TextStyle(
              color: colorScheme.onSurface,
              fontSize: 13,
              fontWeight: FontWeight.w700,
            ),
          ),
        ],
      ),
    );
  }
}

class _StatusBadge extends StatelessWidget {
  const _StatusBadge({
    required this.label,
    required this.color,
  });

  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: 10,
        vertical: 6,
      ),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.10),
        borderRadius: BorderRadius.circular(30),
      ),
      child: Text(
        label,
        style: TextStyle(
          color: color,
          fontSize: 11,
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }
}

class _FinancialHistoryError extends StatelessWidget {
  const _FinancialHistoryError({
    required this.message,
    required this.onRetry,
  });

  final String message;
  final Future<void> Function() onRetry;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(
        vertical: 24,
      ),
      child: Center(
        child: Column(
          children: [
            const Icon(
              Icons.error_outline_rounded,
              color: Color(0xFFB54752),
            ),
            const SizedBox(height: 10),
            Text(
              message,
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 12),
            OutlinedButton(
              onPressed: onRetry,
              child: const Text('Tentar novamente'),
            ),
          ],
        ),
      ),
    );
  }
}
