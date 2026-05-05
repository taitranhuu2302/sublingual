type PageHeaderProps = {
  title: string;
  description: string;
};

export function PageHeader({ title, description }: PageHeaderProps) {
  return (
    <div className="mb-8">
      <h1 className="text-headline-xl mb-2">{title}</h1>
      <p className="text-body-lg text-muted-foreground">{description}</p>
    </div>
  );
}
