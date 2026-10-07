import 'package:flutter/material.dart';

/// An independent scroll position for each form, pane, or popup.
/// Desktop scrollbars use the same controller as their viewport, rather than
/// sharing the route's PrimaryScrollController with adjacent panes.
class MassarScrollView extends StatefulWidget {
  const MassarScrollView({
    super.key,
    required this.child,
    this.controller,
    this.padding = EdgeInsets.zero,
    this.scrollDirection = Axis.vertical,
  });

  final Widget child;
  final ScrollController? controller;
  final EdgeInsetsGeometry padding;
  final Axis scrollDirection;

  @override
  State<MassarScrollView> createState() => _MassarScrollViewState();
}

class _MassarScrollViewState extends State<MassarScrollView> {
  final _ownedController = ScrollController();
  ScrollController get _controller => widget.controller ?? _ownedController;

  @override
  void dispose() {
    _ownedController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => ScrollConfiguration(
    behavior: ScrollConfiguration.of(context).copyWith(scrollbars: false),
    child: Scrollbar(
      controller: _controller,
      thumbVisibility: true,
      interactive: true,
      child: SingleChildScrollView(
        controller: _controller,
        primary: false,
        padding: widget.padding,
        scrollDirection: widget.scrollDirection,
        child: widget.child,
      ),
    ),
  );
}

/// Title, fields, and actions share one viewport. Even at a short window height
/// or large text scale, the last field and the confirmation buttons remain
/// reachable. AlertDialog retains Material route semantics and focus traversal.
class ScrollableMassarDialog extends StatelessWidget {
  const ScrollableMassarDialog({
    super.key,
    this.title,
    required this.content,
    this.actions = const [],
    this.width = 640,
    this.contentPadding = const EdgeInsets.all(24),
    this.scrollController,
  });

  final Widget? title;
  final Widget content;
  final List<Widget> actions;
  final double width;
  final EdgeInsetsGeometry contentPadding;
  final ScrollController? scrollController;

  @override
  Widget build(BuildContext context) => AlertDialog(
    insetPadding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
    contentPadding: EdgeInsets.zero,
    content: SizedBox(
      width: width,
      child: MassarScrollView(
        controller: scrollController,
        padding: contentPadding,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (title != null) ...[
              Semantics(
                namesRoute: true,
                child: DefaultTextStyle(
                  style:
                      DialogTheme.of(context).titleTextStyle ??
                      Theme.of(context).textTheme.headlineSmall!,
                  child: title!,
                ),
              ),
              const SizedBox(height: 20),
            ],
            content,
            if (actions.isNotEmpty) ...[
              const SizedBox(height: 24),
              Wrap(
                alignment: WrapAlignment.end,
                spacing: 8,
                runSpacing: 8,
                children: actions,
              ),
            ],
          ],
        ),
      ),
    ),
  );
}
