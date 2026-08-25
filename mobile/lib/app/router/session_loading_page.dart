import 'package:flutter/material.dart';
import 'package:gymflow/app/session/session_controller.dart';
import 'package:provider/provider.dart';
import 'package:gymflow/app/session/session_status.dart';

class SessionLoadingPage extends StatefulWidget {
  const SessionLoadingPage({super.key});

  @override
  State<SessionLoadingPage> createState() => _SessionLoadingPageState();
}

class _SessionLoadingPageState extends State<SessionLoadingPage> {
  bool _hasError = false;


  @override
  void initState() {
    super.initState();

    WidgetsBinding.instance.addPostFrameCallback((_) {
      _bootstrap();
    });
  }

  Future<void> _bootstrap() async {
    setState(() {
      _hasError = false;
    });

    final sessionController = context.read<SessionController>();

    if (sessionController.status != SessionStatus.unknown) {
      return;
    }

    try {
      await sessionController.bootstrap();
    } catch (_) {
      if (!mounted) {
        return;
      }

      setState(() {
        _hasError = true;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Center(
        child: _hasError
            ? ElevatedButton(
          onPressed: _bootstrap,
          child: const Text('Tentar novamente'),
        )
            : const CircularProgressIndicator(),
      ),
    );
  }
}
