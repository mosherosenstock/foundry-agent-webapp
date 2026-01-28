import type { ReactNode } from 'react';
import { Body1, Subtitle1 } from '@fluentui/react-components';
import { AgentIcon } from '../core/AgentIcon';
import styles from './StarterMessages.module.css';

interface IStarterMessageProps {
  agentName?: string;
  agentDescription?: string;
  agentLogo?: string;
  /**
   * Starter prompts from agent metadata.
   * If not provided, falls back to default prompts.
   * 
   * Configure in Azure AI Foundry portal under agent Configuration > Starter prompts.
   * Prompts are stored as newline-separated text in the "starterPrompts" metadata key.
   */
  starterPrompts?: string[];
  onPromptClick?: (prompt: string) => void;
}

export const StarterMessages = ({
  agentName,
  agentDescription,
  starterPrompts,
  onPromptClick,
}: IStarterMessageProps): ReactNode => {
  // Use agent-provided prompts or fall back to defaults
  const prompts = starterPrompts && starterPrompts.length > 0 
    ? starterPrompts 
    : [
        "¿Cómo puedes ayudarme?",
        "¿Cuáles son tus capacidades?",
        "Cuéntame sobre ti",
      ];

  return (
    <div className={styles.zeroprompt}>
      <div className={styles.content}>
        <AgentIcon
          alt={agentName ?? "Agente"}
          size="large"
          logoUrl="/assets/logo.png"
        />
        <Subtitle1 className={styles.welcome}>
          {agentName ? `¡Hola! Soy ${agentName}` : "¡Hola! ¿Cómo puedo ayudarte hoy?"}
        </Subtitle1>
        {agentDescription && (
          <Body1 className={styles.caption}>Tu compañero inteligente de conversación</Body1>
        )}
      </div>

      {onPromptClick && (
        <ul className={styles.promptList}>
          {prompts.map((prompt, index) => (
            <li key={`prompt-${index}`}>
              <button
                className={styles.promptCard}
                onClick={() => onPromptClick(prompt)}
                type="button"
                title={prompt}
              >
                <span className={styles.promptText}>{prompt}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
};
