import { Spinner } from '@fluentui/react-components';
import { ErrorBoundary } from "./components/core/ErrorBoundary";
import { AgentPreview } from "./components/AgentPreview";
import { useState, useEffect, useCallback } from "react";
import type { IAgentMetadata } from "./types/chat";
import "./App.css";

function App() {
  const [agentMetadata, setAgentMetadata] = useState<IAgentMetadata | null>(null);
  const [isLoadingAgent, setIsLoadingAgent] = useState(true);

  // Wrap fetchAgentMetadata in useCallback to make it stable for the effect
  const fetchAgentMetadata = useCallback(async () => {
    try {
      const apiUrl = import.meta.env.VITE_API_URL || '/api';
      
      const response = await fetch(`${apiUrl}/agent`, {
        headers: {
          'Content-Type': 'application/json'
        }
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }

      const data = await response.json();
      // Override technical agent ID with display name
      data.name = 'Agente Cotizador de Cocinas';
      data.description = data.description || 'Tu compañero inteligente de conversación';
      setAgentMetadata(data);
      
      // Update document title with agent name
      document.title = 'Agente Cotizador de Cocinas';
    } catch (error) {
      console.error('Error fetching agent metadata:', error);
      // Fallback data keeps UI functional on error
      setAgentMetadata({
        id: 'fallback-agent',
        object: 'agent',
        createdAt: Date.now() / 1000,
        name: 'Agente Cotizador de Cocinas',
        description: 'Tu compañero inteligente de conversación',
        model: 'gpt-4o-mini',
        metadata: { logo: 'Avatar_Default.svg' }
      });
      document.title = 'Agente Cotizador de Cocinas';
    } finally {
      setIsLoadingAgent(false);
    }
  }, []);

  useEffect(() => {
    fetchAgentMetadata();
  }, [fetchAgentMetadata]);

  return (
    <ErrorBoundary>
      {isLoadingAgent ? (
        <div className="app-container" style={{ 
          display: 'flex', 
          alignItems: 'center', 
          justifyContent: 'center', 
          height: '100vh', 
          flexDirection: 'column', 
          gap: '1rem' 
        }}>
          <Spinner size="large" />
          <p style={{ margin: 0 }}>
            Loading agent...
          </p>
        </div>
      ) : (
        <div className="app-container">
          {agentMetadata && (
            <AgentPreview 
              agentId={agentMetadata.id}
              agentName={agentMetadata.name}
              agentDescription={agentMetadata.description || undefined}
              agentLogo={agentMetadata.metadata?.logo}
              starterPrompts={agentMetadata.starterPrompts || undefined}
            />
          )}
        </div>
      )}
    </ErrorBoundary>
  );
}

export default App;
