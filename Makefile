.PHONY: dev frontend backend stop

# Run both frontend and backend together
dev: stop
	@echo "🚀 Starting Backend ..."
	@cd backend/src/NaderGorge.API && \
		ASPNETCORE_ENVIRONMENT=E2e /usr/local/share/dotnet/x64/dotnet run  --urls "http://localhost:5245" &
	@echo "⏳ Waiting for backend to start..."
	@sleep 8
	@echo "🎨 Starting Frontend..."
	@cd frontend && npm run dev &
	@echo ""
	@echo "✅ All services running!"
	@echo "   Frontend: http://localhost:3000"
	@echo "   Backend:  http://localhost:5245"
	@echo ""
	@echo "Press Ctrl+C to stop all services"
	@wait

# Run frontend only
frontend:
	@echo "🎨 Starting Frontend..."
	@cd frontend && npm run dev

# Run backend only
backend:
	@echo "🚀 Starting Backend (E2e mode)..."
	@cd backend/src/NaderGorge.API && \
		ASPNETCORE_ENVIRONMENT=E2e /usr/local/share/dotnet/x64/dotnet run --environment E2e --urls "http://localhost:5245"

# Stop all running services
stop:
	@echo "🛑 Stopping any running services..."
	-@lsof -ti:5245 | xargs kill -9 2>/dev/null || true
	-@lsof -ti:3000 | xargs kill -9 2>/dev/null || true
	-@pkill -9 -f "dotnet.*NaderGorge" 2>/dev/null || true
	-@pkill -f "next dev" 2>/dev/null || true
	-@pkill -f "next-server" 2>/dev/null || true
	@sleep 2
	@echo "   Done."
